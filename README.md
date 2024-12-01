# ENusbaum.Torrent - Simple NuGet to create .torrent files

`ENusbaum.Torrent` is a simple NuGet package that allows you to create .torrent files. It is a .NET Library built in C# utilizing dotnet9. 

## Installation

To install `ENusbaum.Torrent`, run the following command in the Package Manager Console:
```
Install-Package ENusbaum.Torrent
```

## Usage

You have two options within `ENusbaum.Torrent` to create a .torrent file. You can either create a .torrent file as a ReadOnlySpan<byte> or have the library write the .torrent file to disk.

### Create a .torrent file as a ReadOnlySpan<byte>
```csharp
public ReadOnlySpan<byte> Create(string inputPath, string torrentName, string trackerAnnounceUrl, PieceSize pieceSize = PieceSize.Auto)
```

### Write the .torrent file to disk
```csharp
public bool CreateFile(string inputPath, string torrentName, string trackerAnnounceUrl, string outputFile, PieceSize pieceSize = PieceSize.Auto)
```

The default Piece Size of `Auto` will automatically determine the best piece size based on the size of the file being used to create the .torrent file with the optimal 2000 pieces. While there is some debate on the number
of pieces to use, 2000 is a good balance between the number of pieces and the size of the pieces.

Currently the maximum piece size supported by this NuGet is 16MiB, but can easily be extended if larger pieces are needed. 