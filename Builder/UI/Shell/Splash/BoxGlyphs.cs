namespace Builder.UI.Shell.Splash;

// Box-drawing glyph for any mix of light and heavy arms. Picking the glyph
// from its arms lets streets, avenues and the causeway meet in clean
// junctions, and lets a line grow half a cell at a time.
internal static class BoxGlyphs
{
    public const int None = 0;
    public const int Light = 1;
    public const int Heavy = 2;

    // Indexed by up * 27 + down * 9 + left * 3 + right
    private const string Table = " ╶╺╴─╼╸╾━╷┌┍┐┬┮┑┭┯╻┎┏┒┰┲┓┱┳╵└┕┘┴┶┙┵┷│├┝┤┼┾┥┽┿╽┟┢┧╁╆┪╅╈╹┖┗┚┸┺┛┹┻╿┞┡┦╀╄┩╃╇┃┠┣┨╂╊┫╉╋";

    public static char For(int up, int down, int left, int right)
    {
        return Table[up * 27 + down * 9 + left * 3 + right];
    }
}
