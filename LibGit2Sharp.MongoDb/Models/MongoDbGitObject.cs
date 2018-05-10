namespace LibGit2Sharp.MongoDb.Models
{
    using System;
    using MongoDB.Bson.Serialization.Attributes;

    public class MongoDbGitObject
    {
        public MongoDbGitObject()
        {
        }

        public MongoDbGitObject(string sha, byte[] data, long length, ObjectType type)
        {
            Id = sha;
            Sha = sha;
            SetData(data);
            Length = length;
            Type = type;
        }

        [BsonId]
        public string Id { get; set; }

        /// <summary>
        /// Data as Base64 string
        /// </summary>
        [BsonElement("data")]
        public string Data { get; set; }

        [BsonElement("sha")]
        public string Sha { get; set; }

        [BsonElement("length")]
        public long Length { get; set; }

        [BsonElement("type")]
        public ObjectType Type { get; set; }

        public byte[] GetDataAsByteArray()
        {
            return Convert.FromBase64String(Data);
        }

        private void SetData(byte[] data)
        {
            Data = Convert.ToBase64String(data);
        }
    }
}
