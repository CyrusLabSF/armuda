using System;
using System.IO;
using UnityEngine;

/// <summary>
/// File system helper for safe deletes, moves, and fixes.
/// </summary>
public static class FileSystemHelper
{
    public static void DirectoryMove(string sourceDirectoryPath, string destDirectoryPath)
    {
        try
        {
            if (!Directory.Exists(destDirectoryPath))
            {
                Directory.Move(sourceDirectoryPath, destDirectoryPath);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectoryPath))
            {
                var newFilePath = Path.Combine(destDirectoryPath, Path.GetFileName(file));
                try
                {
                    if (File.Exists(newFilePath)) File.Delete(newFilePath);
                    File.Move(file, newFilePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Move failed {file} ? {newFilePath}: {e}");
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectoryPath))
            {
                var newDirectoryPath = Path.Combine(destDirectoryPath, Path.GetFileName(directory));
                try
                {
                    if (Directory.Exists(newDirectoryPath)) Directory.Delete(newDirectoryPath, true);
                    Directory.Move(directory, newDirectoryPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Move failed {directory} ? {newDirectoryPath}: {e}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"DirectoryMove failed {sourceDirectoryPath} ? {destDirectoryPath}: {ex}");
        }
    }

    public static void DeleteDirectory(string directoryPath, bool log)
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return;
            if (log) Debug.Log($"Deleting directory: {directoryPath}");
            Directory.Delete(directoryPath, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed DeleteDirectory {directoryPath}: {ex}");
        }
    }

    public static void DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed delete {filePath}: {ex}");
        }
    }

    public static void MoveFile(string sourceFilePath, string destFilePath, bool checkSourceExists)
    {
        try
        {
            if (checkSourceExists && !File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source missing: {sourceFilePath}");

            if (File.Exists(destFilePath)) File.Delete(destFilePath);
            File.Move(sourceFilePath, destFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed move {sourceFilePath} ? {destFilePath}: {ex}");
        }
    }

    public static void FixSpaces(string directoryPath)
    {
        try
        {
            if (directoryPath.Contains("%20"))
            {
                var newPath = directoryPath.Replace("%20", " ");
                Directory.Move(directoryPath, newPath);
                directoryPath = newPath;
            }

            foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
                FixSpaces(subDir);

            foreach (var file in Directory.EnumerateFiles(directoryPath))
            {
                if (file.Contains("%20"))
                {
                    var newFile = file.Replace("%20", " ");
                    File.Move(file, newFile);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"FixSpaces failed {directoryPath}: {ex}");
        }
    }
}
