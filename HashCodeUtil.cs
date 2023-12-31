namespace zinfandel_movie_club;

//based on ideas from https://stackoverflow.com/a/263416
public static class HashCodeUtil
{
    public static int StableHashCode(int v) => new HashCodeBuilder().MixIn(v).Hash;

    public static int StableHashCode(string? s)
    {
        if (s == null) return 0;
        
        var hash = new HashCodeBuilder().MixIn(s.Length);
        foreach (var c in s)
        {
            hash = hash.MixIn(c);
        }

        return hash.Hash;
    }

    public static int Combine(params int[] vs)
    {
        var hash = new HashCodeBuilder().MixIn(vs.Length);
        foreach (var v in vs)
        {
            hash = hash.MixIn(v);
        }

        return hash.Hash;
    }

    private class HashCodeBuilder
    {
        private int _hash;
        public HashCodeBuilder()
        {
            unchecked
            {
                _hash = (int)2166136261;
            }
        }

        public HashCodeBuilder MixIn(int v)
        {
            unchecked
            {
                _hash = (_hash * 16777619) ^ v;
            }

            return this;
        }

        public HashCodeBuilder MixIn(char c)
        {
            return MixIn((int)c);
        }

        public int Hash => _hash;
    }
}