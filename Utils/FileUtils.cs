using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JsonViewer.Utils;

/// <summary>
/// 文件操作工具类
/// </summary>
public static class FileUtils
{
    private static readonly string[] JsonExtensions = { ".json", ".jsonc", ".json5" };
    private static readonly string[] TextExtensions = { ".txt", ".log", ".xml", ".yaml", ".yml" };
    
    /// <summary>
    /// 检查文件是否为JSON文件
    /// </summary>
    public static bool IsJsonFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
            
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return JsonExtensions.Contains(extension);
    }

    /// <summary>
    /// 检查文件是否为文本文件
    /// </summary>
    public static bool IsTextFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
            
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return JsonExtensions.Contains(extension) || TextExtensions.Contains(extension);
    }

    /// <summary>
    /// 获取文件大小的可读格式
    /// </summary>
    public static string GetReadableFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 获取文件大小
    /// </summary>
    public static long GetFileSize(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 检查文件是否存在且可读
    /// </summary>
    public static bool IsFileAccessible(string filePath)
    {
        try
        {
            return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全读取文件内容
    /// </summary>
    public static async Task<string> ReadFileContentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!IsFileAccessible(filePath))
            throw new FileNotFoundException($"文件不存在或无法访问: {filePath}");

        try
        {
            // 检测文件编码
            var encoding = DetectFileEncoding(filePath);
            
            // 对于大文件，使用流式读取
            var fileSize = GetFileSize(filePath);
            if (fileSize > 50 * 1024 * 1024) // 50MB
            {
                return await ReadLargeFileAsync(filePath, encoding, cancellationToken);
            }
            
            return await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new IOException($"读取文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 读取大文件
    /// </summary>
    private static async Task<string> ReadLargeFileAsync(string filePath, Encoding encoding, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192);
        using var reader = new StreamReader(fileStream, encoding);
        
        var content = new StringBuilder();
        var buffer = new char[8192];
        int bytesRead;
        
        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Append(buffer, 0, bytesRead);
        }
        
        return content.ToString();
    }

    /// <summary>
    /// 检测文件编码
    /// </summary>
    public static Encoding DetectFileEncoding(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            // 读取前几个字节来检测BOM
            var bom = new byte[4];
            var bytesRead = fileStream.Read(bom, 0, 4);
            
            if (bytesRead >= 3)
            {
                // UTF-8 BOM
                if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return Encoding.UTF8;
                    
                // UTF-16 LE BOM
                if (bom[0] == 0xFF && bom[1] == 0xFE)
                    return Encoding.Unicode;
                    
                // UTF-16 BE BOM
                if (bom[0] == 0xFE && bom[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
            }
            
            if (bytesRead >= 4)
            {
                // UTF-32 LE BOM
                if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                    return Encoding.UTF32;
                    
                // UTF-32 BE BOM
                if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                    return new UTF32Encoding(true, true);
            }
            
            // 没有BOM，尝试检测编码
            fileStream.Seek(0, SeekOrigin.Begin);
            return DetectEncodingFromContent(fileStream);
        }
        catch
        {
            return Encoding.UTF8; // 默认使用UTF-8
        }
    }

    /// <summary>
    /// 从内容检测编码
    /// </summary>
    private static Encoding DetectEncodingFromContent(FileStream fileStream)
    {
        var buffer = new byte[Math.Min(1024, fileStream.Length)];
        fileStream.Read(buffer, 0, buffer.Length);
        
        // 检查是否为有效的UTF-8
        if (IsValidUtf8(buffer))
            return Encoding.UTF8;
            
        // 检查是否包含ASCII字符
        if (buffer.All(b => b < 128))
            return Encoding.ASCII;
            
        // 默认使用系统默认编码
        return Encoding.Default;
    }

    /// <summary>
    /// 检查字节数组是否为有效的UTF-8
    /// </summary>
    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            var decoder = Encoding.UTF8.GetDecoder();
            var chars = new char[bytes.Length];
            decoder.Convert(bytes, 0, bytes.Length, chars, 0, chars.Length, true, out _, out _, out bool completed);
            return completed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全写入文件
    /// </summary>
    public static async Task WriteFileContentAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 创建临时文件
            var tempFilePath = filePath + ".tmp";
            
            // 写入临时文件
            await File.WriteAllTextAsync(tempFilePath, content, Encoding.UTF8, cancellationToken);
            
            // 原子性替换
            if (File.Exists(filePath))
            {
                var backupPath = filePath + ".bak";
                File.Move(filePath, backupPath);
                
                try
                {
                    File.Move(tempFilePath, filePath);
                    File.Delete(backupPath); // 删除备份文件
                }
                catch
                {
                    // 恢复备份
                    if (File.Exists(backupPath))
                    {
                        File.Move(backupPath, filePath);
                    }
                    throw;
                }
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"写入文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 计算文件哈希值
    /// </summary>
    public static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();
            
            var hashBytes = await Task.Run(() => sha256.ComputeHash(fileStream), cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            throw new IOException($"计算文件哈希失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取临时文件路径
    /// </summary>
    public static string GetTempFilePath(string extension = ".tmp")
    {
        var tempDir = Path.GetTempPath();
        var fileName = $"JsonViewer_{Guid.NewGuid():N}{extension}";
        return Path.Combine(tempDir, fileName);
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    public static void CleanupTempFiles(ILogger? logger = null)
    {
        try
        {
            var tempDir = Path.GetTempPath();
            var tempFiles = Directory.GetFiles(tempDir, "JsonViewer_*")
                .Where(f => File.GetCreationTime(f) < DateTime.Now.AddHours(-1)) // 清理1小时前的临时文件
                .ToList();
                
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    File.Delete(tempFile);
                    logger?.LogDebug("已删除临时文件: {TempFile}", tempFile);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "删除临时文件失败: {TempFile}", tempFile);
                }
            }
            
            if (tempFiles.Count > 0)
            {
                logger?.LogInformation("已清理 {Count} 个临时文件", tempFiles.Count);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "清理临时文件时发生错误");
        }
    }

    /// <summary>
    /// 获取安全的文件名
    /// </summary>
    public static string GetSafeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "untitled";
            
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeFileName = new StringBuilder();
        
        foreach (var c in fileName)
        {
            if (invalidChars.Contains(c))
            {
                safeFileName.Append('_');
            }
            else
            {
                safeFileName.Append(c);
            }
        }
        
        var result = safeFileName.ToString().Trim();
        
        // 确保文件名不为空且不是保留名称
        if (string.IsNullOrEmpty(result) || IsReservedFileName(result))
        {
            result = "untitled";
        }
        
        return result;
    }

    /// <summary>
    /// 检查是否为Windows保留文件名
    /// </summary>
    private static bool IsReservedFileName(string fileName)
    {
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        return reservedNames.Contains(fileName.ToUpperInvariant());
    }

    /// <summary>
    /// 获取唯一文件名
    /// </summary>
    public static string GetUniqueFileName(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
            return fileName;
            
        var fullPath = Path.Combine(directory, fileName);
        if (!File.Exists(fullPath))
            return fileName;
            
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        
        int counter = 1;
        string newFileName;
        
        do
        {
            newFileName = $"{nameWithoutExtension} ({counter}){extension}";
            fullPath = Path.Combine(directory, newFileName);
            counter++;
        }
        while (File.Exists(fullPath));
        
        return newFileName;
    }

    /// <summary>
    /// 检查磁盘空间是否足够
    /// </summary>
    public static bool HasSufficientDiskSpace(string path, long requiredBytes)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
            return drive.AvailableFreeSpace >= requiredBytes;
        }
        catch
        {
            return true; // 无法检测时假设有足够空间
        }
    }

    /// <summary>
    /// 获取文件的MIME类型
    /// </summary>
    public static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".json" => "application/json",
            ".jsonc" => "application/json",
            ".json5" => "application/json5",
            ".txt" => "text/plain",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "application/x-yaml",
            ".log" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}