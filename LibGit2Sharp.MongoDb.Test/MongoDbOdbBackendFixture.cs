namespace LibGit2Sharp.MongoDb.Test
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Models;
    using MongoDB.Driver;
    using Xunit;

    public class MongoDbOdbBackendFixture : IDisposable
    {
        private static readonly string TempPath = Path.Combine(Path.GetTempPath(), "MongoDb.OdbBackend");

        public MongoDbOdbBackendFixture()
        {
            Directory.CreateDirectory(TempPath);
        }

        public void Dispose()
        {
            Helpers.DeleteDirectory(TempPath);
        }

        [Fact]
        public void CanReadSimpleBlobs()
        {
            const string id = "9daeafb9864cf43055ae93beb0afd6c7d144bfa4";
            var backend = new MongoDbOdbBackendForTest("test-repo", "alex");
            backend.Collection.DeleteOne(o => o.Id == id);

            backend.Collection.InsertOne(new MongoDbGitObject
            {
                Id = id,
                Data = "dGVzdAo=",
                Sha = "9daeafb9864cf43055ae93beb0afd6c7d144bfa4",
                Length = 5,
                Type = ObjectType.Tree
            });

            var objectId = new ObjectId("9daeafb9864cf43055ae93beb0afd6c7d144bfa4");
            var ret = backend.Read(objectId, out var stream, out var objectType);

            Assert.Equal(/*(int)OdbBackend.ReturnCode.GIT_OK*/ 0, ret);
            Assert.NotNull(stream);
            stream.Dispose();
            Assert.Equal(ObjectType.Tree, objectType);

            backend.Collection.DeleteOne(o => o.Id == id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanStageAFileAndLookupTheGeneratedBlob(bool useMongoDb)
        {
            using (var repo = Build(useMongoDb))
            {
                const string path = "test.txt";
                const string content = "test\n";

                var objectId = new ObjectId("9daeafb9864cf43055ae93beb0afd6c7d144bfa4");

                Assert.False(repo.ObjectDatabase.Contains(objectId));
                Assert.Null(repo.Lookup<Blob>(objectId));

                Helpers.Touch(repo.Info.WorkingDirectory, path, content);
                Commands.Stage(repo, path);

                var ie = repo.Index[path];
                Assert.NotNull(ie);

                Assert.True(repo.ObjectDatabase.Contains(ie.Id));

                // TODO: Maybe lookup of a blob should only trigger read_header()
                var blob = repo.Lookup<Blob>(objectId);
                Assert.Equal(content.Length, blob.Size);
                Assert.Equal(content, blob.GetContentText());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanGeneratePredictableObjectShas(bool useMongoDb)
        {
            using (var repo = Build(useMongoDb))
            {
                AddCommitToRepo(repo);

                AssertGeneratedShas(repo);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanCreateLargeBlobs(bool useMongoDb)
        {
            using (var repo = Build(useMongoDb))
            {
                var zeros = new string('0', (128 * 1024) + 3);
                var objectId = new ObjectId("3e7b4813e7b08195c7f59ca8efb6069fc9cf21a7");

                var blob = CreateBlob(repo, zeros);
                Assert.Equal(objectId, blob.Id);

                Assert.True(repo.ObjectDatabase.Contains(objectId));

                blob = repo.Lookup<Blob>(objectId);
                Assert.Equal(zeros, blob.GetContentText());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanEnumerateGitObjects(bool useMongoDb)
        {
            using (var repo = Build(useMongoDb))
            {
                AddCommitToRepo(repo);

                var count = repo.ObjectDatabase.Count();

                Assert.Equal(3, count);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanLookupByShortObjectId(bool useMongoDb)
        {
            /*
             * $ echo "aabqhq" | git hash-object -t blob --stdin
             * dea509d0b3cb8ee0650f6ca210bc83f4678851ba
             *
             * $ echo "aaazvc" | git hash-object -t blob --stdin
             * dea509d097ce692e167dfc6a48a7a280cc5e877e
             */

            using (var repo = Build(useMongoDb))
            {
                var blob1 = CreateBlob(repo, "aabqhq\n");
                Assert.Equal("dea509d0b3cb8ee0650f6ca210bc83f4678851ba", blob1.Sha);
                var blob2 = CreateBlob(repo, "aaazvc\n");
                Assert.Equal("dea509d097ce692e167dfc6a48a7a280cc5e877e", blob2.Sha);

                Assert.Equal(2, repo.ObjectDatabase.Count());

                Assert.Throws<AmbiguousSpecificationException>(() => repo.Lookup("dea509d0"));
                Assert.Throws<AmbiguousSpecificationException>(() => repo.Lookup("dea509d"));

                Assert.Equal(blob1, repo.Lookup("dea509d0b"));
                Assert.Equal(blob2, repo.Lookup("dea509d09"));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanFetch(bool useMongoDb)
        {
            using (var repo = Build(useMongoDb)) // TODO this doesn't seem to touch ES
            {
                Assert.Empty(repo.ObjectDatabase);

                repo.Network.Remotes.Add("origin", "https://github.com/libgit2/TestGitRepository");
                var origin = repo.Network.Remotes.First(r => r.Name == "origin");
                var refSpecs = origin.FetchRefSpecs.Select(s => s.Specification).ToList();
                Commands.Fetch(repo, "origin", refSpecs, null, string.Empty);
                Commands.Fetch(repo, "origin", refSpecs, new FetchOptions { TagFetchMode = TagFetchMode.All }, string.Empty);

                Assert.Equal(70, repo.ObjectDatabase.Count());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanShortenObjectIdentifier(bool useMongoDb)
        {
            /*
             * $ echo "aabqhq" | git hash-object -t blob --stdin
             * dea509d0b3cb8ee0650f6ca210bc83f4678851ba
             * 
             * $ echo "aaazvc" | git hash-object -t blob --stdin
             * dea509d097ce692e167dfc6a48a7a280cc5e877e
             */

            using (var repo = Build(useMongoDb))
            {
                repo.Config.Set("core.abbrev", 4);

                Blob blob1 = CreateBlob(repo, "aabqhq\n");
                Assert.Equal("dea509d0b3cb8ee0650f6ca210bc83f4678851ba", blob1.Sha);

                Assert.Equal("dea5", repo.ObjectDatabase.ShortenObjectId(blob1));
                Assert.Equal("dea509d0b3cb", repo.ObjectDatabase.ShortenObjectId(blob1, 12));
                Assert.Equal("dea509d0b3cb8ee0650f6ca210bc83f4678851b", repo.ObjectDatabase.ShortenObjectId(blob1, 39));

                Blob blob2 = CreateBlob(repo, "aaazvc\n");
                Assert.Equal("dea509d09", repo.ObjectDatabase.ShortenObjectId(blob2));
                Assert.Equal("dea509d09", repo.ObjectDatabase.ShortenObjectId(blob2, 4));
                Assert.Equal("dea509d0b", repo.ObjectDatabase.ShortenObjectId(blob1));
                Assert.Equal("dea509d0b", repo.ObjectDatabase.ShortenObjectId(blob1, 7));

                Assert.Equal("dea509d0b3cb", repo.ObjectDatabase.ShortenObjectId(blob1, 12));
                Assert.Equal("dea509d097ce", repo.ObjectDatabase.ShortenObjectId(blob2, 12));
            }
        }

        [Fact]
        public void CanReopenMongoDbBackedRepository()
        {
            string indexName;
            string gitDirPath;

            const string blobSha = "dea509d0b3cb8ee0650f6ca210bc83f4678851ba";

            using (var repo = Build(true))
            {
                var blob = CreateBlob(repo, "aabqhq\n");
                Assert.Equal(blobSha, blob.Sha);

                indexName = IndexNameFrom(repo);
                gitDirPath = repo.Info.Path;
            }

            using (var repo = new Repository(gitDirPath))
            {
                Assert.Null(repo.Lookup<Blob>(blobSha));

                SetMongoDbOdbBackend(repo, indexName);

                var blob = repo.Lookup<Blob>(blobSha);
                Assert.Equal(blobSha, blob.Sha);
            }
        }

        private static void AddCommitToRepo(IRepository repo)
        {
            const string path = "test.txt";
            const string content = "test\n";

            Helpers.Touch(repo.Info.WorkingDirectory, path, content);
            Commands.Stage(repo, path);

            var author = new Signature("nulltoken", "emeric.fermas@gmail.com", DateTimeOffset.Parse("Wed, Dec 14 2011 08:29:03 +0100"));
            repo.Commit("Initial commit", author, author);
        }

        private static void AssertGeneratedShas(IRepository repo)
        {
            var commit = repo.Commits.Single();
            Assert.Equal("1fe3126578fc4eca68c193e4a3a0a14a0704624d", commit.Sha);
            var tree = commit.Tree;
            Assert.Equal("2b297e643c551e76cfa1f93810c50811382f9117", tree.Sha);

            var blob = tree.Single().Target;
            Assert.IsAssignableFrom<Blob>(blob);
            Assert.Equal("9daeafb9864cf43055ae93beb0afd6c7d144bfa4", blob.Sha);
        }

        private static Repository InitNewRepository(string baseDir)
        {
            var tempPath = Path.Combine(baseDir, Guid.NewGuid().ToString());
            var path = Repository.Init(tempPath);

            var repository = new Repository(path);

            return repository;
        }

        private static void SetMongoDbOdbBackend(IRepository repository, string repoName)
        {
            var backend = new MongoDbOdbBackend(repoName, "master");
            repository.ObjectDatabase.AddBackend(backend, 5);
        }

        private static string IndexNameFrom(IRepository repository)
        {
            var dir = new DirectoryInfo(repository.Info.WorkingDirectory);
            return dir.Name.Substring(0, 7);
        }

        private static Blob CreateBlob(IRepository repo, string content)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                return repo.ObjectDatabase.CreateBlob(stream);
            }
        }

        private static Repository Build(bool useMongoDb)
        {
            var repository = InitNewRepository(TempPath);

            if (!useMongoDb)
            {
                return repository;
            }

            SetMongoDbOdbBackend(repository, IndexNameFrom(repository));

            return repository;
        }
    }
}
