using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Library;

public static class LibraryFolderPresets
{
    public static readonly IReadOnlyList<LibraryFolderPreset> All =
    [
        new LibraryFolderPreset
        {
            Kind = LibraryFolderKind.Music,
            DefaultName = "Music",
            TranscodeGroup = FileGroups.Audio,
            AutoTagGroup = FileGroups.Audio,
        },
        new LibraryFolderPreset
        {
            Kind = LibraryFolderKind.Movies,
            DefaultName = "Movies",
            TranscodeGroup = FileGroups.Videos,
        },
        new LibraryFolderPreset { Kind = LibraryFolderKind.Pictures, DefaultName = "Pictures" },
        new LibraryFolderPreset { Kind = LibraryFolderKind.Videos, DefaultName = "Videos" },
        new LibraryFolderPreset { Kind = LibraryFolderKind.Documents, DefaultName = "Documents" },
        new LibraryFolderPreset
        {
            Kind = LibraryFolderKind.Notes,
            DefaultName = "Notes",
            SystemKind = DirectorySystemKind.Notes,
        },
    ];

    public static LibraryFolderPreset Get(LibraryFolderKind kind)
    {
        return All.FirstOrDefault(p => p.Kind == kind)
               ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown library folder kind");
    }
}
