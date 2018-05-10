namespace LibGit2Sharp.MongoDb
{
    using System;
    using System.IO;
    using System.Linq;
    using Extentions;
    using Models;
    using MongoDB.Driver;

    public class MongoDbOdbBackend : OdbBackend
    {
        protected readonly IMongoCollection<MongoDbGitObject> MongoCollection;

        public MongoDbOdbBackend(string databaseName, string collectionName, MongoDatabaseSettings databaseSettings = null, MongoCollectionSettings collectionSettings = null)
        {
            var mongoClient = new MongoClient();
            var mongoDatabase = mongoClient.GetDatabase(databaseName, databaseSettings);
            MongoCollection = mongoDatabase.GetCollection<MongoDbGitObject>(collectionName, collectionSettings);
        }

        public MongoDbOdbBackend(MongoClientSettings settings, string databaseName, string collectionName, MongoDatabaseSettings databaseSettings = null, MongoCollectionSettings collectionSettings = null)
        {
            var mongoClient = new MongoClient(settings);
            var mongoDatabase = mongoClient.GetDatabase(databaseName, databaseSettings);
            MongoCollection = mongoDatabase.GetCollection<MongoDbGitObject>(collectionName, collectionSettings);
        }

        protected override OdbBackendOperations SupportedOperations => OdbBackendOperations.Read |
                                                                       OdbBackendOperations.ReadPrefix |
                                                                       OdbBackendOperations.ReadHeader |
                                                                       OdbBackendOperations.Write |
                                                                       OdbBackendOperations.Exists |
                                                                       OdbBackendOperations.ExistsPrefix |
                                                                       OdbBackendOperations.ForEach;

        public override int Read(ObjectId id, out UnmanagedMemoryStream data, out ObjectType objectType)
        {
            var gitObject = MongoCollection.Find(g => g.Id == id.ToString()).FirstOrDefault();

            if (gitObject == null)
            {
                objectType = ObjectType.Blob;
                data = null;
                return (int)ReturnCode.GIT_ENOTFOUND;
            }

            objectType = gitObject.Type;

            data = Allocate(gitObject.Length);
            var bytes = gitObject.GetDataAsByteArray();
            data.Write(bytes, 0, bytes.Length);

            return (int)ReturnCode.GIT_OK;
        }

        public override int ReadPrefix(string shortSha, out ObjectId oid, out UnmanagedMemoryStream data, out ObjectType objectType)
        {
            oid = null;
            data = null;
            objectType = default(ObjectType);

            var gitObjects = MongoCollection.Find(g => g.Id.Contains(shortSha)).ToList();

            if (!gitObjects.Any())
            {
                return (int)ReturnCode.GIT_ENOTFOUND;
            }

            if (gitObjects.Count != 1)
            {
                return (int)ReturnCode.GIT_EAMBIGUOUS;
            }

            var gitObject = gitObjects.Single();

            oid = new ObjectId(gitObject.Id);
            objectType = gitObject.Type;
            data = Allocate(gitObject.Length);
            var bytes = gitObject.GetDataAsByteArray();
            data.Write(bytes, 0, bytes.Length);

            return (int)ReturnCode.GIT_OK;
        }

        public override int ReadHeader(ObjectId id, out int length, out ObjectType objectType)
        {
            var gitObject = MongoCollection.Find(g => g.Id == id.ToString()).FirstOrDefault();

            if (gitObject == null)
            {
                objectType = ObjectType.Blob;
                length = 0;
                return (int)ReturnCode.GIT_ENOTFOUND;
            }

            objectType = gitObject.Type;
            length = (int)gitObject.Length;

            return (int)ReturnCode.GIT_OK;
        }

        public override int Write(ObjectId id, Stream dataStream, long length, ObjectType objectType)
        {
            var gitObject = new MongoDbGitObject(id.Sha, dataStream.ReadAsBytes(), length, objectType);
            MongoCollection.InsertOne(gitObject);
            return (int)ReturnCode.GIT_OK;
        }

        public override int ReadStream(ObjectId id, out OdbBackendStream stream)
        {
            throw new NotImplementedException("ReadStream");
        }

        public override int WriteStream(long length, ObjectType objectType, out OdbBackendStream stream)
        {
            throw new NotImplementedException("WriteStream");
        }

        public override bool Exists(ObjectId id)
        {
            var gitObject = MongoCollection.Find(g => g.Id == id.ToString()).FirstOrDefault();
            return gitObject != null;
        }

        public override int ExistsPrefix(string shortSha, out ObjectId found)
        {
            found = null;

            var gitObjects = MongoCollection.Find(g => g.Id.Contains(shortSha)).ToList();

            if (!gitObjects.Any())
            {
                return (int)ReturnCode.GIT_ENOTFOUND;
            }

            if (gitObjects.Count != 1)
            {
                return (int)ReturnCode.GIT_EAMBIGUOUS;
            }

            var gitObject = gitObjects.Single();

            found = new ObjectId(gitObject.Id);

            return (int)ReturnCode.GIT_OK;
        }

        public override int ForEach(ForEachCallback callback)
        {
            // ToDo: Paging?
            var gitObjects = MongoCollection.FindSync(FilterDefinition<MongoDbGitObject>.Empty).ToList();

            if (!gitObjects.Any())
            {
                return (int)ReturnCode.GIT_OK;
            }

            var callbackSuccessful = gitObjects.Select(g => callback(new ObjectId(g.Sha))).Any(ret => ret != (int)ReturnCode.GIT_OK);

            if (!callbackSuccessful)
            {
                return (int)ReturnCode.GIT_EUSER;
            }

            return (int)ReturnCode.GIT_OK;
        }
    }
}
