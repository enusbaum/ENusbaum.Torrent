namespace ENusbaum.Torrent
{
    /// <summary>
    ///     Enum for the size of the pieces in the torrent in Kibibytes/Mebibytes (KiB) with the Enum value being the size in bytes
    /// </summary>
    public enum PieceSize
    {
        Auto = -1,
        Size64KiB = 65536,
        Size128KiB = 131072,
        Size256KiB = 262144,
        Size512KiB = 524288,
        Size1MiB = 1048576,
        Size2MiB = 2097152,
        Size4MiB = 4194304,
        Size8MiB = 8388608,
        Size16MiB = 16777216
    }
}
