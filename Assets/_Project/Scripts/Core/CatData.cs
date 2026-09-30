namespace CatMerge.Core
{
    public sealed class CatData
    {
        public int Id { get; }
        public int Level { get; }

        public CatData(int id, int level)
        {
            Id = id;
            Level = level;
        }
    }
}
