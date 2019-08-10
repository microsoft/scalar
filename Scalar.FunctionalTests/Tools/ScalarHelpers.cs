using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.Tests.Should;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Scalar.FunctionalTests.Tools
{
    public static class ScalarHelpers
    {
        public const string ModifiedPathsNewLine = "\r\n";
        public const string PlaceholderFieldDelimiter = "\0";

        public static readonly string BackgroundOpsFile = Path.Combine("databases", "BackgroundGitOperations.dat");
        public static readonly string PlaceholderListFile = Path.Combine("databases", "PlaceholderList.dat");
        public static readonly string RepoMetadataName = Path.Combine("databases", "RepoMetadata.dat");

        private const string ModifedPathsLineAddPrefix = "A ";
        private const string ModifedPathsLineDeletePrefix = "D ";

        private const string DiskLayoutMajorVersionKey = "DiskLayoutVersion";
        private const string DiskLayoutMinorVersionKey = "DiskLayoutMinorVersion";
        private const string LocalCacheRootKey = "LocalCacheRoot";
        private const string GitObjectsRootKey = "GitObjectsRoot";
        private const string BlobSizesRootKey = "BlobSizesRoot";

        public static string ConvertPathToGitFormat(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, TestConstants.GitPathSeparator);
        }

        public static void SaveDiskLayoutVersion(string dotScalarRoot, string majorVersion, string minorVersion)
        {
            SavePersistedValue(dotScalarRoot, DiskLayoutMajorVersionKey, majorVersion);
            SavePersistedValue(dotScalarRoot, DiskLayoutMinorVersionKey, minorVersion);
        }

        public static void GetPersistedDiskLayoutVersion(string dotScalarRoot, out string majorVersion, out string minorVersion)
        {
            majorVersion = GetPersistedValue(dotScalarRoot, DiskLayoutMajorVersionKey);
            minorVersion = GetPersistedValue(dotScalarRoot, DiskLayoutMinorVersionKey);
        }

        public static void SaveLocalCacheRoot(string dotScalarRoot, string value)
        {
            SavePersistedValue(dotScalarRoot, LocalCacheRootKey, value);
        }

        public static string GetPersistedLocalCacheRoot(string dotScalarRoot)
        {
            return GetPersistedValue(dotScalarRoot, LocalCacheRootKey);
        }

        public static void SaveGitObjectsRoot(string dotScalarRoot, string value)
        {
            SavePersistedValue(dotScalarRoot, GitObjectsRootKey, value);
        }

        public static string GetPersistedGitObjectsRoot(string dotScalarRoot)
        {
            return GetPersistedValue(dotScalarRoot, GitObjectsRootKey);
        }

        public static string GetPersistedBlobSizesRoot(string dotScalarRoot)
        {
            return GetPersistedValue(dotScalarRoot, BlobSizesRootKey);
        }

        public static void SQLiteBlobSizesDatabaseHasEntry(string blobSizesDbPath, string blobSha, long blobSize)
        {
            RunSqliteCommand(blobSizesDbPath, command =>
            {
                SqliteParameter shaParam = command.CreateParameter();
                shaParam.ParameterName = "@sha";
                command.CommandText = "SELECT size FROM BlobSizes WHERE sha = (@sha)";
                command.Parameters.Add(shaParam);
                shaParam.Value = StringToShaBytes(blobSha);

                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    reader.Read().ShouldBeTrue();
                    reader.GetInt64(0).ShouldEqual(blobSize);
                }

                return true;
            });
        }

        public static string GetAllSQLitePlaceholdersAsString(string placeholdersDbPath)
        {
            return RunSqliteCommand(placeholdersDbPath, command =>
                {
                    command.CommandText = "SELECT path, pathType, sha FROM Placeholder";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        StringBuilder sb = new StringBuilder();
                        while (reader.Read())
                        {
                            sb.Append(reader.GetString(0));
                            sb.Append(PlaceholderFieldDelimiter);
                            sb.Append(reader.GetByte(1));
                            sb.Append(PlaceholderFieldDelimiter);
                            if (!reader.IsDBNull(2))
                            {
                                sb.Append(reader.GetString(2));
                                sb.Append(PlaceholderFieldDelimiter);
                            }

                            sb.AppendLine();
                        }

                        return sb.ToString();
                    }
                });
        }

        public static void AddPlaceholderFolder(string placeholdersDbPath, string path, int pathType)
        {
            RunSqliteCommand(placeholdersDbPath, command =>
            {
                command.CommandText = "INSERT OR REPLACE INTO Placeholder (path, pathType, sha) VALUES (@path, @pathType, NULL)";
                command.Parameters.AddWithValue("@path", path);
                command.Parameters.AddWithValue("@pathType", pathType);
                return command.ExecuteNonQuery();
            });
        }

        public static string ReadAllTextFromWriteLockedFile(string filename)
        {
            // File.ReadAllText and others attempt to open for read and FileShare.None, which always fail on
            // the placeholder db and other files that open for write and only share read access
            using (StreamReader reader = new StreamReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }

        public static void ModifiedPathsContentsShouldEqual(ScalarFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem, string contents)
        {
            string modifedPathsContents = GetModifiedPathsContents(enlistment, fileSystem);
            modifedPathsContents.ShouldEqual(contents);
        }

        public static void ModifiedPathsShouldContain(ScalarFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem, params string[] gitPaths)
        {
            string modifedPathsContents = GetModifiedPathsContents(enlistment, fileSystem);
            string[] modifedPathLines = modifedPathsContents.Split(new[] { ModifiedPathsNewLine }, StringSplitOptions.None);
            foreach (string gitPath in gitPaths)
            {
                modifedPathLines.ShouldContain(path => path.Equals(ModifedPathsLineAddPrefix + gitPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static void ModifiedPathsShouldNotContain(ScalarFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem, params string[] gitPaths)
        {
            string modifedPathsContents = GetModifiedPathsContents(enlistment, fileSystem);
            string[] modifedPathLines = modifedPathsContents.Split(new[] { ModifiedPathsNewLine }, StringSplitOptions.None);
            foreach (string gitPath in gitPaths)
            {
                modifedPathLines.ShouldNotContain(
                    path =>
                    {
                        return path.Equals(ModifedPathsLineAddPrefix + gitPath, StringComparison.OrdinalIgnoreCase) ||
                               path.Equals(ModifedPathsLineDeletePrefix + gitPath, StringComparison.OrdinalIgnoreCase);
                    });
            }
        }

        public static string GetInternalParameter(string maintenanceJob = "null", string packfileMaintenanceBatchSize = "null")
        {
            return $"\"{{\\\"ServiceName\\\":\\\"{ScalarServiceProcess.TestServiceName}\\\"," +
                    "\\\"StartedByService\\\":false," +
                    $"\\\"MaintenanceJob\\\":{maintenanceJob}," +
                    $"\\\"PackfileMaintenanceBatchSize\\\":{packfileMaintenanceBatchSize}}}\"";
        }

        private static string GetModifiedPathsContents(ScalarFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem)
        {
            enlistment.WaitForBackgroundOperations();
            string modifiedPathsDatabase = Path.Combine(enlistment.DotScalarRoot, TestConstants.Databases.ModifiedPaths);
            modifiedPathsDatabase.ShouldBeAFile(fileSystem);
            return ScalarHelpers.ReadAllTextFromWriteLockedFile(modifiedPathsDatabase);
        }

        private static T RunSqliteCommand<T>(string sqliteDbPath, Func<SqliteCommand, T> runCommand)
        {
            string connectionString = $"data source={sqliteDbPath}";
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (SqliteCommand command = connection.CreateCommand())
                {
                    return runCommand(command);
                }
            }
        }

        private static byte[] StringToShaBytes(string sha)
        {
            byte[] shaBytes = new byte[20];

            string upperCaseSha = sha.ToUpper();
            int stringIndex = 0;
            for (int i = 0; i < 20; ++i)
            {
                stringIndex = i * 2;
                char firstChar = sha[stringIndex];
                char secondChar = sha[stringIndex + 1];
                shaBytes[i] = (byte)(CharToByte(firstChar) << 4 | CharToByte(secondChar));
            }

            return shaBytes;
        }

        private static byte CharToByte(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }

            if (c >= 'A' && c <= 'F')
            {
                return (byte)(10 + (c - 'A'));
            }

            Assert.Fail($"Invalid character c: {c}");

            return 0;
        }

        private static string GetPersistedValue(string dotScalarRoot, string key)
        {
            string metadataPath = Path.Combine(dotScalarRoot, RepoMetadataName);
            string json;
            using (FileStream fs = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(fs))
            {
                while (!reader.EndOfStream)
                {
                    json = reader.ReadLine();
                    json.Substring(0, 2).ShouldEqual("A ");

                    KeyValuePair<string, string> kvp = JsonConvert.DeserializeObject<KeyValuePair<string, string>>(json.Substring(2));
                    if (kvp.Key == key)
                    {
                        return kvp.Value;
                    }
                }
            }

            return null;
        }

        private static void SavePersistedValue(string dotScalarRoot, string key, string value)
        {
            string metadataPath = Path.Combine(dotScalarRoot, RepoMetadataName);

            Dictionary<string, string> repoMetadata = new Dictionary<string, string>();
            string json;
            using (FileStream fs = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(fs))
            {
                while (!reader.EndOfStream)
                {
                    json = reader.ReadLine();
                    json.Substring(0, 2).ShouldEqual("A ");

                    KeyValuePair<string, string> kvp = JsonConvert.DeserializeObject<KeyValuePair<string, string>>(json.Substring(2));
                    repoMetadata.Add(kvp.Key, kvp.Value);
                }
            }

            repoMetadata[key] = value;

            string newRepoMetadataContents = string.Empty;

            foreach (KeyValuePair<string, string> kvp in repoMetadata)
            {
                newRepoMetadataContents += "A " + JsonConvert.SerializeObject(kvp).Trim() + "\r\n";
            }

            File.WriteAllText(metadataPath, newRepoMetadataContents);
        }
    }
}
