﻿using System.Security.Cryptography;
using System.Text;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.Updating;

public static class FileUpdater
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(FileUpdater)); 
    
    public static void UpdateFiles(string fileListUrl, string path, string description)
    {
        try
        {
            UpdateFilesPrivate(fileListUrl, path, description);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Failed to update files in {path}: " + exception.Message);
        }
    }

    private static void UpdateFilesPrivate(string fileListUrl, string path, string description)
    {
        _logger.Info($"Downloading {description} file list {fileListUrl} ...");

        byte[] fileListBytes = HttpUtil.DownloadFile(fileListUrl);
        string str = Encoding.UTF8.GetString(fileListBytes);
        using MemoryStream memoryStream = new(fileListBytes);
        FileList fileList = JsonUtil.DeserializeStream<FileList>(memoryStream);

        string baseUrl = fileList.BaseUrl;
        if (!baseUrl.EndsWith('/'))
            baseUrl += '/';

        bool updated = false;
        bool error = false;
        foreach (FileListFile fileListFile in fileList.Files)
        {
            string destFilePath = Path.Combine(path, fileListFile.Name);
            string? hash = CalculateHash(destFilePath);
            if (string.Equals(hash, fileListFile.Hash))
                continue;
            
            string url = baseUrl + fileListFile.Name;
            _logger.Info($"Downloading {description} file {url} ...");
            byte[] data;
            try
            {
                data = HttpUtil.DownloadFile(url);
            }
            catch (Exception exception)
            {
                _logger.Warn($"Error downloading {description} file {url}: {exception.Message}");
                error = true;
                continue;
            }

            try
            {
                if (destFilePath.EndsWith(".gz"))
                {
                    string fn = destFilePath[..^3];
                    if (File.Exists(fn))
                        File.Delete(fn);
                }

                File.WriteAllBytes(destFilePath, data);
                updated = true;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Error saving {description} file {destFilePath}: {exception.Message}");
                error = true;
            }
        }

        if (!updated)
        {
            if (!error)
                _logger.Info($"{description} is up to date.");
        }
        else
        {
            _logger.Info($"{description} has been updated.");
        }
    }

    private static string? CalculateHash(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(fileStream);
        return string.Concat(hash.Select(x => x.ToString("X2")));
    }
}