using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    private const string JsKeywords =
        "break case catch class const continue debugger default delete do else export extends finally for " +
        "function if import in instanceof let new of return super switch this throw try typeof var void while " +
        "with yield async await static get set from as";

    public static readonly LanguageSpec JavaScript = new()
    {
        Name = "javascript",
        Keywords = Words(JsKeywords),
        Literals = Words("true false null undefined NaN Infinity"),
        LineComments = ["//"],
        BlockComments = CBlock,
        MultilineQuotes = "`",
        TypeHeuristic = true
    };

    public static readonly LanguageSpec TypeScript = new()
    {
        Name = "typescript",
        Keywords = Words(JsKeywords + " interface type enum implements namespace declare abstract readonly private " +
                         "protected public keyof satisfies infer is asserts string number boolean any unknown never " +
                         "object symbol bigint"),
        Literals = Words("true false null undefined NaN Infinity"),
        LineComments = ["//"],
        BlockComments = CBlock,
        MultilineQuotes = "`",
        TypeHeuristic = true
    };

    public static readonly LanguageSpec CSharp = new()
    {
        Name = "csharp",
        Keywords = Words(
            "abstract as base bool break byte case catch char checked class const continue decimal default delegate " +
            "do double else enum event explicit extern finally fixed float for foreach goto if implicit in int " +
            "interface internal is lock long namespace new object operator out override params private protected " +
            "public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw " +
            "try typeof uint ulong unchecked unsafe ushort using virtual void volatile while var async await record " +
            "init required partial get set yield when where with nameof global file scoped"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        CSharpStrings = true,
        TripleQuotes = true,
        HashDirectives = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Python = new()
    {
        Name = "python",
        Keywords = Words(
            "and as assert async await break class continue def del elif else except finally for from global if " +
            "import in is lambda nonlocal not or pass raise return try while with yield match case"),
        Literals = Words("True False None self cls"),
        LineComments = ["#"],
        TripleQuotes = true,
        StringPrefixes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Java = new()
    {
        Name = "java",
        Keywords = Words(
            "abstract assert boolean break byte case catch char class const continue default do double else enum " +
            "extends final finally float for goto if implements import instanceof int interface long native new " +
            "package private protected public return short static strictfp super switch synchronized this throw " +
            "throws transient try void volatile while var record sealed permits yield"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Go = new()
    {
        Name = "go",
        Keywords = Words(
            "break case chan const continue default defer else fallthrough for func go goto if import interface map " +
            "package range return select struct switch type var string int int8 int16 int32 int64 uint uint8 uint16 " +
            "uint32 uint64 float32 float64 bool byte rune error any"),
        Literals = Words("true false nil iota"),
        LineComments = ["//"],
        BlockComments = CBlock,
        MultilineQuotes = "`",
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Rust = new()
    {
        Name = "rust",
        Keywords = Words(
            "as async await break const continue crate dyn else enum extern fn for if impl in let loop match mod " +
            "move mut pub ref return self Self static struct super trait type unsafe use where while"),
        Literals = Words("true false"),
        LineComments = ["//"],
        BlockComments = CBlock,
        Quotes = "\"",
        TypeHeuristic = true
    };

    private const string CKeywords =
        "auto break case char const continue default do double else enum extern float for goto if inline int long " +
        "register restrict return short signed sizeof static struct switch typedef union unsigned void volatile while";

    public static readonly LanguageSpec C = new()
    {
        Name = "c",
        Keywords = Words(CKeywords),
        Literals = Words("true false NULL"),
        LineComments = ["//"],
        BlockComments = CBlock,
        HashDirectives = true
    };

    public static readonly LanguageSpec Cpp = new()
    {
        Name = "cpp",
        Keywords = Words(CKeywords +
                         " class namespace template typename public private protected virtual override new delete this " +
                         "using bool try catch throw constexpr noexcept operator final explicit friend mutable"),
        Literals = Words("true false NULL nullptr"),
        LineComments = ["//"],
        BlockComments = CBlock,
        HashDirectives = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Php = new()
    {
        Name = "php",
        Keywords = Words(
            "abstract and array as break callable case catch class clone const continue declare default die do echo " +
            "else elseif empty enddeclare endfor endforeach endif endswitch endwhile eval exit extends final finally " +
            "fn for foreach function global goto if implements include include_once instanceof insteadof interface " +
            "isset list match namespace new or print private protected public readonly require require_once return " +
            "static switch throw trait try unset use var while xor yield"),
        Literals = Words("true false null"),
        LineComments = ["//", "#"],
        BlockComments = CBlock,
        Quotes = "\"'",
        HashNeedsBoundary = true,
        Variables = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Swift = new()
    {
        Name = "swift",
        Keywords = Words(
            "associatedtype class deinit enum extension fileprivate func import init inout internal let open operator " +
            "private protocol public rethrows static struct subscript typealias var break case continue default " +
            "defer do else fallthrough for guard if in repeat return switch where while as Any catch is super self " +
            "Self throw throws try async await actor some any"),
        Literals = Words("true false nil"),
        LineComments = ["//"],
        BlockComments = CBlock,
        Quotes = "\"",
        TripleQuotes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Scala = new()
    {
        Name = "scala",
        Keywords = Words(
            "abstract case catch class def do else enum export extends final finally for forSome given if implicit " +
            "import lazy match new object override package private protected return sealed super then this throw " +
            "trait try type val var while with yield using"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        TripleQuotes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Kotlin = new()
    {
        Name = "kotlin",
        Keywords = Words(
            "abstract actual annotation as break by catch class companion const constructor continue crossinline data " +
            "delegate do dynamic else enum expect external field file final finally for fun get if import in infix " +
            "init inline inner interface internal is lateinit noinline object open operator out override package param " +
            "private property protected public receiver reified return sealed set setparam super suspend tailrec this " +
            "throw try typealias typeof val value var vararg when where while"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        Quotes = "\"",
        TripleQuotes = true,
        Variables = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Dart = new()
    {
        Name = "dart",
        Keywords = Words(
            "abstract as assert async await break case catch class const continue covariant default deferred do dynamic " +
            "else enum export extends extension external factory final finally for Function get hide if implements " +
            "import in interface is late library mixin new on operator part required rethrow return set show static " +
            "super switch sync this throw try typedef var void while with yield"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        Quotes = "\"",
        TripleQuotes = true,
        TypeHeuristic = true
    };
}
