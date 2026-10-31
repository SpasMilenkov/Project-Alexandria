using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    public static readonly LanguageSpec Sql = new()
    {
        Name = "sql",
        Keywords = Words(
            "select from where and or not insert into values update set delete create table alter drop index view " +
            "join inner outer left right full cross on group by order having limit offset union all distinct as " +
            "case when then else end is in exists between like primary key foreign references default unique " +
            "constraint check begin commit rollback with returning asc desc top add column if", ignoreCase: true),
        Literals = Words("true false null", ignoreCase: true),
        LineComments = ["--"],
        BlockComments = CBlock,
        Quotes = "\"'`",
        DoubledQuotes = true,
        BackslashEscapes = false
    };

    public static readonly LanguageSpec Bash = new()
    {
        Name = "bash",
        Keywords = Words(
            "if then else elif fi for while until do done case esac in function select time return exit break " +
            "continue export local readonly declare unset source alias set shift"),
        Literals = Words("true false"),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Quotes = "\"",
        RawQuotes = "'",
        Variables = true
    };

    public static readonly LanguageSpec PowerShell = new()
    {
        Name = "powershell",
        Keywords = Words(
            "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally for " +
            "foreach from function hidden if in param process return static switch throw trap try until using var while",
            ignoreCase: true),
        Literals = Words("true false null", ignoreCase: true),
        LineComments = ["#"],
        BlockComments = [("<#", "#>")],
        Quotes = "\"'",
        BackslashEscapes = false,
        HashNeedsBoundary = true,
        Variables = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Batch = new()
    {
        Name = "batch",
        Keywords = Words(
            "call cd chdir cls copy del dir do echo else endlocal errorlevel exist exit for goto if in md mkdir move " +
            "not pause popd pushd rd rem ren rename set setlocal shift start type", ignoreCase: true),
        Literals = Words("on off", ignoreCase: true),
        LineComments = ["::"],
        Quotes = "\"",
        BackslashEscapes = false,
        PercentVariables = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Ruby = new()
    {
        Name = "ruby",
        Keywords = Words(
            "alias and begin break case class def defined do else elsif end ensure for if in module next not or redo " +
            "rescue retry return self super then undef unless until when while yield require require_relative include " +
            "extend prepend private protected public attr_reader attr_writer attr_accessor"),
        Literals = Words("true false nil"),
        LineComments = ["#"],
        Quotes = "\"'",
        HashNeedsBoundary = true,
        Variables = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Lua = new()
    {
        Name = "lua",
        Keywords = Words(
            "and break do else elseif end false for function goto if in local nil not or repeat return then true until while"),
        Literals = Words("true false nil"),
        LineComments = ["--"],
        BlockComments = [("--[[", "]]")],
        Quotes = "\"'",
        TypeHeuristic = false
    };

    public static readonly LanguageSpec R = new()
    {
        Name = "r",
        Keywords = Words(
            "if else repeat while function for in next break TRUE FALSE NULL Inf NaN NA"),
        Literals = Words("TRUE FALSE NULL Inf NaN NA"),
        LineComments = ["#"],
        Quotes = "\"'",
        FnHeuristic = true
    };

    public static readonly LanguageSpec Julia = new()
    {
        Name = "julia",
        Keywords = Words(
            "baremodule begin break catch const continue do else elseif end export finally for function global if " +
            "import in isa let local macro module quote return struct try using while where abstract mutable primitive " +
            "type"),
        Literals = Words("true false nothing missing"),
        LineComments = ["#"],
        BlockComments = [("#=", "=#")],
        Quotes = "\"'",
        TripleQuotes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec FSharp = new()
    {
        Name = "fsharp",
        Keywords = Words(
            "abstract and as assert base begin class default delegate do done downcast downto elif else end exception " +
            "extern false finally fixed for fun function global if in inherit inline interface internal lazy let match " +
            "member module mutable namespace new not null of open or override private public rec return select static " +
            "struct then to true try type upcast use val void when while with yield"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = [("(*", "*)")],
        Quotes = "\"",
        TripleQuotes = true,
        CSharpStrings = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec VisualBasic = new()
    {
        Name = "vb",
        Keywords = Words(
            "addhandler addressof alias and andalso as boolean byref byte byval call case catch cbool cbyte cchar " +
            "cdate cdbl cdec char cint class clng cobj const continue csbyte cshort csng cstr ctype cuint culng " +
            "cushort date decimal declare default delegate dim directcast do double each else elseif end enum erase " +
            "error event exit false finally for friend function get gettype global gosub goto handles if implements " +
            "imports in inherits integer interface is isnot let lib like long loop me mod module mustinherit " +
            "mustoverride mybase myclass namespace narrowing new next not nothing notinheritable notoverridable " +
            "object of on operator option optional or orelse overloads overridable overrides paramarray partial " +
            "private property protected public raiseevent readonly redim rem removehandler resume return sbyte select " +
            "set shadows shared short single static step stop string structure sub synclock then throw to true try " +
            "trycast typeof uinteger ulong ushort using variant wend when while widening with withevents writeonly xor",
            ignoreCase: true),
        Literals = Words("true false nothing", ignoreCase: true),
        LineComments = ["'"],
        Quotes = "\"",
        BackslashEscapes = false,
        DoubledQuotes = true,
        TypeHeuristic = true
    };
}
