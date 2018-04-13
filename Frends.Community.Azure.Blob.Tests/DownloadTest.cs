﻿using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Frends.Community.Azure.Blob.Tests
{
    [TestFixture]
    class DownloadTest
    {
        /// <summary>
        /// Container name for tests
        /// </summary>
        private readonly string _containerName = "test-container";

        /// <summary>
        /// Connection string for Azure Storage Emulator
        /// </summary>
        private readonly string _connectionString = "UseDevelopmentStorage=true";

        /// <summary>
        /// Some random file for test purposes
        /// </summary>
        private readonly string _testBlob = "test-blob.txt";

        /// <summary>
        /// Some random file for test purposes
        /// </summary>
        private string _testFilePath = $@"{AppDomain.CurrentDomain.BaseDirectory}\TestFiles\TestFile.xml";

        private string _destinationDirectory;

        private SourceProperties _source;
        private DestinationFileProperties _destination;
        //private BlobContentProperties _content;
        private CancellationToken _cancellationToken;

        [SetUp]
        public async Task TestSetup()
        {
            _destinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_destinationDirectory);

            // task properties
            _source = new SourceProperties { ConnectionString = _connectionString, BlobName = _testBlob, BlobType = AzureBlobType.Block, ContainerName = _containerName };
            _destination = new DestinationFileProperties { Directory = _destinationDirectory, FileExistsOperation = FileExistsAction.Overwrite };
            _cancellationToken = new CancellationToken();


            // setup test material for download tasks

            var container = Utils.GetBlobContainer(_connectionString, _containerName);
            await container.CreateIfNotExistsAsync();

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(_testBlob);

            await blockBlob.UploadFromFileAsync(_testFilePath);
        }

        [TearDown]
        public async Task Cleanup()
        {
            // delete whole container after running tests
            CloudBlobContainer container = Utils.GetBlobContainer(_connectionString, _containerName);
            await container.DeleteIfExistsAsync();

            // delete test files and folders
            if (Directory.Exists(_destinationDirectory))
                Directory.Delete(_destinationDirectory, true);
        }

        [Test]
        public async Task ReadBlobContentAsync_ReturnsContentString()
        {
            var result = await DownloadTask.ReadBlobContentAsync(_source, _cancellationToken);

            Assert.IsTrue(result.Content.Contains(@"<input>WhatHasBeenSeenCannotBeUnseen</input>"));
        }

        [Test]
        public async Task DownloadBlobAsync_WritesBlobToFile()
        {
            var result = await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);

            Assert.IsTrue(File.Exists(result.FullPath));
            var fileContent = File.ReadAllText(result.FullPath);
            Assert.IsTrue(fileContent.Contains(@"<input>WhatHasBeenSeenCannotBeUnseen</input>"));
        }

        [Test]
        public async Task DownloadBlobAsync_ThrowsExceptionIfDestinationFileExists()
        {
            await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);
            _destination.FileExistsOperation = FileExistsAction.Error;

            Assert.ThrowsAsync<IOException>(async () => await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken));
        }

        [Test]
        public async Task DownloadBlobAsync_RenamesFileIfExists()
        {
            await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);
            _destination.FileExistsOperation = FileExistsAction.Rename;

            var result = await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);

            Assert.AreEqual("test-blob(1).txt", result.FileName);
        }

        [Test]
        public async Task DownloadBlobAsync_OverwritesFileIfExists()
        {
            // download file with same name couple of time
            _destination.FileExistsOperation = FileExistsAction.Overwrite;
            await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);
            await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);
            await DownloadTask.DownloadBlobAsync(_source, _destination, _cancellationToken);

            // only one file should exist in destination folder
            Assert.AreEqual(1, Directory.GetFiles(_destinationDirectory).Length);
        }
    }
}