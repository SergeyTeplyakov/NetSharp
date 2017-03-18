using System;

namespace NetSharp.UnitTest.Infrastructure
{
    [Serializable]
    class FileData
    {
        public string FileName { get; private set; }
        public long Length { get; private set; }

        public FileData() { }

        public FileData(string fileName, long length)
        {
            FileName = fileName;
            Length = length;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            FileData fileData = obj as FileData;

            if (fileData == null)
                return false;

            if (Object.ReferenceEquals(this, obj))
                return true;

            return this.FileName == fileData.FileName && this.Length == fileData.Length;
        }
    }
}
