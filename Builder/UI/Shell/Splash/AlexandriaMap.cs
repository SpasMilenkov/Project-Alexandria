namespace Builder.UI.Shell.Splash;

// Alexandria around 30 BCE, traced from Otto Puchstein's reconstruction of
// the city plan (c. 1890). Coordinates are in map units: pixels of that plan
// at 1:100,000, rotated by GridBearing so the street grid, which the city's
// founders laid out at that angle to north, runs straight across the
// terminal. North therefore points up and slightly to the right.
internal static class AlexandriaMap
{
    public const double GridBearing = 22.75;

    // The plan's scale bar: one mile is 278 units
    public const double UnitsPerKilometre = 173;

    // The area every splash shows: Pharos to the Canopic Gate, the sea above
    // the lighthouse to the shore of Lake Mareotis
    public const double Left = -170;
    public const double Right = 1010;
    public const double Top = 380;
    public const double Bottom = 1300;

    // Where the success page's strip of city is centred
    public const double CityMiddle = 860;

    public static readonly (double U, double V) Lighthouse = (436, 439);

    // Land outlines; a point is on land when it falls inside an odd number of them
    public static readonly int[][] Land =
    [
        // Mainland, Pharos and the Heptastadion causeway
        [
           232, 390, 238, 388, 250, 404, 266, 404, 272, 410, 266, 420, 246, 420, 244, 438, 258, 440, 268, 450, 268,
           490, 276, 538, 272, 586, 266, 592, 242, 592, 236, 598, 236, 614, 306, 768, 378, 768, 394, 756, 446, 748,
           452, 742, 452, 722, 476, 706, 476, 690, 468, 674, 444, 646, 450, 632, 464, 642, 464, 654, 476, 674, 498,
           688, 550, 688, 566, 684, 614, 656, 630, 656, 640, 638, 640, 614, 612, 546, 630, 528, 646, 528, 656, 538,
           660, 546, 656, 574, 684, 598, 692, 614, 692, 634, 698, 640, 742, 640, 758, 652, 774, 652, 802, 636, 838,
           644, 874, 624, 942, 620, 1130, 576, 1318, 652, 1320, 1334, 1314, 1336, 1274, 1320, 894, 1160, 846, 1160,
           762, 1220, 734, 1220, 706, 1236, 682, 1240, 666, 1228, 646, 1228, 630, 1220, 606, 1224, 554, 1220, 548,
           1210, 556, 1194, 574, 1176, 588, 1170, 586, 1152, 470, 1148, 472, 1182, 484, 1202, 484, 1214, 478, 1220,
           402, 1216, 370, 1192, 342, 1184, 266, 1184, 222, 1192, 208, 1206, 122, 1420, -418, 1420, -420, 690,
           -402, 692, -286, 744, -246, 756, -234, 768, -202, 776, 58, 888, 78, 888, 90, 876, 106, 876, 108, 858,
           116, 850, 116, 834, 138, 804, 182, 788, 188, 794, 190, 812, 214, 812, 238, 804, 262, 780, 284, 778, 284,
           762, 220, 610, 210, 600, 198, 600, 166, 580, 54, 548, 26, 548, 18, 556, -22, 568, -30, 576, -86, 584,
           -122, 616, -130, 616, -136, 602, -128, 594, -128, 578, -122, 572, -106, 572, -68, 546, -68, 514, -46,
           496, -38, 500, -14, 496, 34, 500, 50, 484, 70, 480, 138, 516, 170, 516, 202, 500, 220, 478, 232, 450,
           232, 430, 222, 420, 214, 420, 208, 410
        ],
        // Antirrhodus
        [
           528, 622, 542, 620, 564, 642, 564, 654, 554, 656, 538, 636, 522, 636, 506, 652, 504, 642
        ],
        // Islet beside the lighthouse
        [
           356, 406, 370, 404, 376, 410, 376, 422, 370, 428, 358, 428, 352, 422
        ],
    ];

    // Streets running along the grid, by their distance down the map, with
    // the stretch each one covers. The widest is the Canopic Way; the
    // conjectured ones are those the plan draws dashed.
    public static readonly MapStreet[] LongStreets =
    [
        new() { Position = 870, From = 148, To = 932, Major = true },
        new() { Position = 673, From = 668, To = 934 },
        new() { Position = 720, From = 503, To = 979 },
        new() { Position = 767, From = 416, To = 980 },
        new() { Position = 819, From = 168, To = 979 },
        new() { Position = 915, From = 144, To = 911 },
        new() { Position = 947, From = 197, To = 897 },
        new() { Position = 968, From = 212, To = 886, Conjectured = true },
        new() { Position = 998, From = 197, To = 663, Conjectured = true },
    ];

    // Streets crossing the grid, by their distance across the map. The
    // longest runs from the palace harbour through the Canopic Way.
    public static readonly MapStreet[] CrossStreets =
    [
        new() { Position = 670, From = 651, To = 1010, Major = true },
        new() { Position = 263, From = 828, To = 1055 },
        new() { Position = 285, From = 827, To = 1045 },
        new() { Position = 305, From = 828, To = 1043 },
        new() { Position = 337, From = 828, To = 1043 },
        new() { Position = 397, From = 789, To = 1043 },
        new() { Position = 416, From = 804, To = 907 },
        new() { Position = 451, From = 777, To = 1044 },
        new() { Position = 505, From = 729, To = 1044 },
        new() { Position = 530, From = 728, To = 811 },
        new() { Position = 560, From = 730, To = 1043 },
        new() { Position = 611, From = 710, To = 1011 },
        new() { Position = 638, From = 709, To = 939 },
        new() { Position = 729, From = 661, To = 1010 },
        new() { Position = 785, From = 661, To = 999 },
        new() { Position = 840, From = 661, To = 988 },
        new() { Position = 230, From = 829, To = 1077, Conjectured = true },
    ];

    // The Heptastadion, from the mainland out to Pharos island
    public static readonly (double U, double V)[] Causeway = [(295, 780), (267, 700), (223, 600)];

    public static readonly MapLabel[] Labels =
    [
        new() { Text = "MEDITERRANEAN", U = 860, V = 470, OnWater = true },
        new() { Text = "GREAT HARBOUR", U = 415, V = 548, OnWater = true },
        new() { Text = "EUNOSTOS", U = 68, V = 652, OnWater = true },
        new() { Text = "PHAROS", U = 107, V = 522, OnWater = false },
        new() { Text = "LAKE MAREOTIS", U = 600, V = 1270, OnWater = true },
    ];
}
