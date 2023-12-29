namespace zinfandel_movie_club;

public interface ISeededRandom
{
    public uint NextUnsignedInt(uint previous);

    public long Next(long previous)
    {
        return long.CreateSaturating(NextUnsignedInt(uint.CreateSaturating(previous)));
    }

    // returns a random positive integer based on the previous seed
    // because this only returns positive integers, half of the possible values will never be returned
    public int NextPositiveInt(int previous)
    {
        uint previous_ = (uint) previous;
        uint next = uint.MaxValue;
        do
        {
            next = NextUnsignedInt(previous_);
            previous_ = next;
        } while (next > (uint)int.MaxValue);

        return (int)next;
    }

    public int NextInt(int previous)
    {
        var bytes = BitConverter.GetBytes(previous);
        var previous_ = BitConverter.ToUInt32(bytes);
        var next_ = NextUnsignedInt(previous_);
        bytes = BitConverter.GetBytes(next_);
        return BitConverter.ToInt32(bytes);
    }


    public int NextInRange(int previous, int min, int max)
    {
        if (min >= 0 && previous >= 0)
        {
            var next = NextPositiveInt(previous);
            return next % (max - min + 1);
        }
        else
        {
            var next = NextInt(previous);
            return next % (max - min + 1);
        }
    }
}

// https://en.wikipedia.org/wiki/Lehmer_random_number_generator
public class LehmerRandomGenerator : ISeededRandom
{
    private const uint a = 48_271;
    private const uint m = 0x7fffffff;

    public uint NextUnsignedInt(uint previous) => (previous * a) % m;
}