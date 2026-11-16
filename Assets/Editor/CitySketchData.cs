// Generated from docs/city_sketch.png by the transcription script (scratchpad map/layout.py).
// Units: metres, X east, Z north, origin = bottom-left of the sketch. Scale bar 40 m = 200 px.
using UnityEngine;

namespace Taxi.EditorTools
{
    public static class CitySketchData
    {
        static Vector2 V(float x, float z) => new Vector2(x, z);

        public static void Fill(CityLayout L)
        {
            L.roadWidth = 10.0f;
            L.bounds = Rect.MinMaxRect(44f, -2f, 286f, 200f);
            L.park = Rect.MinMaxRect(95f, 79f, 189f, 115f);
            L.parkingLot = Rect.MinMaxRect(54f, 18f, 80f, 40f);
            L.playerSpawn = V(71f, 33f); L.playerSpawnYaw = 90f;
            L.carSpawn = V(76f, 34f); L.carSpawnYaw = 0f;
            L.zones = new[] {
                new CityLayout.Zone { id = 1, name = "Zone 1", price = 0, rect = Rect.MinMaxRect(47f, 3f, 130f, 83f) },
                new CityLayout.Zone { id = 2, name = "Zone 2", price = 150, rect = Rect.MinMaxRect(130f, 0f, 224f, 84f) },
                new CityLayout.Zone { id = 3, name = "Zone 3", price = 300, rect = Rect.MinMaxRect(158f, 83f, 278f, 174f) },
                new CityLayout.Zone { id = 4, name = "Zone 4", price = 600, rect = Rect.MinMaxRect(40f, 83f, 158f, 197f) },
            };
            L.roads = new[] {
                new CityLayout.Road { name = "West", points = new[] { V(52f, 8f), V(52f, 137f) } },
                new CityLayout.Road { name = "South", points = new[] { V(52f, 8f), V(193f, 8f) } },
                new CityLayout.Road { name = "East", points = new[] { V(193f, 8f), V(193f, 78f), V(193f, 117f) } },
                new CityLayout.Road { name = "ParkSouth", points = new[] { V(52f, 78f), V(193f, 78f) } },
                new CityLayout.Road { name = "Main117", points = new[] { V(52f, 117f), V(191f, 117f) } },
                new CityLayout.Road { name = "EastLink", points = new[] { V(191f, 117f), V(204f, 109f), V(250f, 109f) } },
                new CityLayout.Road { name = "ParkPath", points = new[] { V(93f, 88f), V(115f, 91f), V(140f, 95f), V(165f, 99f), V(191f, 105f), V(204f, 109f) } },
                new CityLayout.Road { name = "ParkWest", points = new[] { V(93f, 78f), V(93f, 117f) } },
                new CityLayout.Road { name = "Z88", points = new[] { V(52f, 88f), V(93f, 88f) } },
                new CityLayout.Road { name = "DiagNW", points = new[] { V(52f, 123f), V(88f, 170f) } },
                new CityLayout.Road { name = "K91", points = new[] { V(91f, 117f), V(91f, 170f) } },
                new CityLayout.Road { name = "Top170", points = new[] { V(88f, 170f), V(118f, 170f) } },
                new CityLayout.Road { name = "M118", points = new[] { V(118f, 170f), V(118f, 140f) } },
                new CityLayout.Road { name = "N140", points = new[] { V(118f, 140f), V(162f, 140f) } },
                new CityLayout.Road { name = "O162", points = new[] { V(162f, 140f), V(162f, 117f) } },
                new CityLayout.Road { name = "X76", points = new[] { V(76f, 78f), V(76f, 28f) } },
                new CityLayout.Road { name = "Z54", points = new[] { V(52f, 54f), V(64f, 54f), V(83f, 52f), V(109f, 38.5f) } },
                new CityLayout.Road { name = "BigDiag", points = new[] { V(74f, 26f), V(125f, 45f) } },
                new CityLayout.Road { name = "Z45", points = new[] { V(125f, 45f), V(193f, 45f) } },
                new CityLayout.Road { name = "X125", points = new[] { V(125f, 78f), V(125f, 8f) } },
                new CityLayout.Road { name = "DiagSE1", points = new[] { V(135f, 45f), V(150f, 32f), V(150f, 8f) } },
                new CityLayout.Road { name = "DiagSE2", points = new[] { V(163f, 45f), V(169f, 24f), V(193f, 8f) } },
            };
            L.rings = new[] {
                new CityLayout.Ring { name = "RingNW", center = V(52f, 145f), radius = 8.0f },
                new CityLayout.Ring { name = "RingE", center = V(258f, 109f), radius = 8.0f },
            };
            L.buildings = new[] {
                new CityLayout.Building { name = "WestEdge", zone = 0, height = 12.0f, polygon = new[] { V(1f, 3f), V(44f, 3f), V(44f, 180f), V(1f, 180f) } },
                new CityLayout.Building { name = "SouthEdge", zone = 0, height = 12.0f, polygon = new[] { V(50f, 0f), V(192f, 0f), V(192f, 3f), V(50f, 3f) } },
                new CityLayout.Building { name = "B2", zone = 4, height = 10.3f, polygon = new[] { V(48f, 158f), V(68f, 158f), V(82f, 178f), V(82f, 191f), V(48f, 191f) } },
                new CityLayout.Building { name = "B1", zone = 4, height = 9.1f, polygon = new[] { V(88f, 176f), V(116f, 176f), V(116f, 193f), V(88f, 193f) } },
                new CityLayout.Building { name = "B21", zone = 4, height = 12.6f, polygon = new[] { V(127f, 149f), V(156f, 149f), V(156f, 172f), V(127f, 172f) } },
                new CityLayout.Building { name = "B3", zone = 4, height = 8.5f, polygon = new[] { V(100f, 123f), V(109f, 123f), V(109f, 160f), V(100f, 160f) } },
                new CityLayout.Building { name = "B8", zone = 4, height = 11.8f, polygon = new[] { V(114f, 126f), V(156f, 126f), V(156f, 132f), V(114f, 132f) } },
                new CityLayout.Building { name = "B4", zone = 4, height = 10.6f, polygon = new[] { V(63f, 123f), V(83f, 123f), V(83f, 149f) } },
                new CityLayout.Building { name = "B9", zone = 4, height = 8.4f, polygon = new[] { V(58f, 94f), V(84f, 94f), V(84f, 108f), V(58f, 108f) } },
                new CityLayout.Building { name = "B7", zone = 3, height = 11.6f, polygon = new[] { V(168f, 126f), V(198f, 126f), V(198f, 136f), V(168f, 136f) } },
                new CityLayout.Building { name = "B6", zone = 3, height = 8.3f, polygon = new[] { V(206f, 115f), V(219f, 115f), V(219f, 139f), V(206f, 139f) } },
                new CityLayout.Building { name = "B5", zone = 3, height = 11.0f, polygon = new[] { V(225f, 118f), V(244f, 118f), V(244f, 142f), V(225f, 142f) } },
                new CityLayout.Building { name = "B22", zone = 3, height = 8.5f, polygon = new[] { V(268f, 106f), V(282f, 106f), V(282f, 128f), V(268f, 128f) } },
                new CityLayout.Building { name = "B23", zone = 3, height = 8.6f, polygon = new[] { V(199f, 84f), V(211f, 84f), V(211f, 101f), V(199f, 101f) } },
                new CityLayout.Building { name = "B24", zone = 3, height = 11.0f, polygon = new[] { V(215f, 85f), V(245f, 85f), V(245f, 100f), V(215f, 100f) } },
                new CityLayout.Building { name = "B25", zone = 3, height = 13.8f, polygon = new[] { V(250f, 80f), V(280f, 80f), V(280f, 99f), V(250f, 99f) } },
                new CityLayout.Building { name = "B15", zone = 2, height = 8.9f, polygon = new[] { V(135f, 52f), V(184f, 52f), V(184f, 69.5f), V(135f, 69.5f) } },
                new CityLayout.Building { name = "B13", zone = 2, height = 9.6f, polygon = new[] { V(200f, 50f), V(214f, 50f), V(214f, 72f), V(200f, 72f) } },
                new CityLayout.Building { name = "B17", zone = 2, height = 12.4f, polygon = new[] { V(201f, 8f), V(216f, 8f), V(216f, 40f), V(201f, 40f) } },
                new CityLayout.Building { name = "B18", zone = 2, height = 14.6f, polygon = new[] { V(172f, 40f), V(182f, 40f), V(188f, 33f), V(188f, 21f), V(178f, 27f) } },
                new CityLayout.Building { name = "B20", zone = 2, height = 12.0f, polygon = new[] { V(131f, 16.5f), V(145f, 16.5f), V(145f, 23.8f), V(131f, 35.9f) } },
                new CityLayout.Building { name = "B11", zone = 1, height = 10.8f, polygon = new[] { V(81.5f, 57f), V(88f, 57f), V(88f, 73f), V(81.5f, 73f) } },
                new CityLayout.Building { name = "B12", zone = 1, height = 14.8f, polygon = new[] { V(58f, 59f), V(67f, 59f), V(67f, 73f), V(58f, 73f) } },
                new CityLayout.Building { name = "B14", zone = 1, height = 8.3f, polygon = new[] { V(98f, 52f), V(107f, 48f), V(118f, 52f), V(118f, 72f), V(98f, 72f) } },
                new CityLayout.Building { name = "B19", zone = 1, height = 14.0f, polygon = new[] { V(72f, 14f), V(116f, 14f), V(116f, 32.6f), V(72f, 16.3f) } },
                new CityLayout.Building { name = "Garage", zone = 1, height = 6.0f, polygon = new[] { V(56f, 41f), V(70f, 41f), V(70f, 47f), V(56f, 47f) } },
            };
            L.pockets = new[] {
                new CityLayout.Pocket { name = "Stop_01", zone = 4, position = V(104.1f, 163.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_02", zone = 4, position = V(124.5f, 163.7f), yaw = 180.0f },
                new CityLayout.Pocket { name = "Stop_03", zone = 4, position = V(84.5f, 151.8f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_04", zone = 4, position = V(52f, 154.5f), yaw = 90f },
                new CityLayout.Pocket { name = "Stop_05", zone = 4, position = V(111.5f, 147.9f), yaw = 180.0f },
                new CityLayout.Pocket { name = "Stop_06", zone = 4, position = V(149.3f, 146.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_07", zone = 4, position = V(125.4f, 133.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_08", zone = 4, position = V(97.5f, 132.7f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_09", zone = 3, position = V(182.7f, 123.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_10", zone = 4, position = V(123.2f, 123.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_11", zone = 3, position = V(258f, 118.5f), yaw = 90f },
                new CityLayout.Pocket { name = "Stop_12", zone = 3, position = V(172.1f, 123.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_13", zone = 4, position = V(70.2f, 110.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_14", zone = 3, position = V(232.4f, 115.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_15", zone = 3, position = V(232.1f, 102.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_16", zone = 4, position = V(86.5f, 102.0f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_17", zone = 3, position = V(186.5f, 97.3f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_18", zone = 3, position = V(186.5f, 90.5f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_19", zone = 2, position = V(161.2f, 71.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_20", zone = 1, position = V(69.5f, 70.4f), yaw = 180.0f },
                new CityLayout.Pocket { name = "Stop_21", zone = 2, position = V(186.5f, 68.9f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_22", zone = 2, position = V(186.5f, 58.0f), yaw = 0.0f },
                new CityLayout.Pocket { name = "Stop_23", zone = 1, position = V(94.8f, 53.2f), yaw = 117.4f },
                new CityLayout.Pocket { name = "Stop_24", zone = 2, position = V(139.8f, 32.2f), yaw = 130.9f },
                // Stop_25 and Stop_27 sit behind a road barrier with no way in: kept in the data, disabled.
                new CityLayout.Pocket { name = "Stop_25", zone = 2, position = V(160.4f, 30.5f), yaw = 164.1f, disabled = true },
                new CityLayout.Pocket { name = "Stop_26", zone = 1, position = V(118.5f, 17.4f), yaw = 180.0f },
                new CityLayout.Pocket { name = "Stop_27", zone = 2, position = V(156.5f, 17.8f), yaw = 180.0f, disabled = true },
                // Stop_28 removed (author's request).
                new CityLayout.Pocket { name = "Stop_29", zone = 2, position = V(139.7f, 14.5f), yaw = 90.0f },
                // Start area (not in the sketch, added for Zone 1 variety): taxi depot in front of the garage, parking lot.
                new CityLayout.Pocket { name = "Stop_Depot", zone = 1, position = V(63f, 38.5f), yaw = 90.0f },
                new CityLayout.Pocket { name = "Stop_Parking", zone = 1, position = V(63f, 26f), yaw = 90.0f },
            };
            L.barriers = new[] {
                new CityLayout.Barrier { name = "Bar_South_Z2", closesZone = 2, position = V(133.0f, 8.0f), yaw = 90.0f },
                new CityLayout.Barrier { name = "Bar_Z45_Z2", closesZone = 2, position = V(133.0f, 45.0f), yaw = 90.0f },
                new CityLayout.Barrier { name = "Bar_ParkS_Z2", closesZone = 2, position = V(133.0f, 78.0f), yaw = 90.0f },
                new CityLayout.Barrier { name = "Bar_West_Z4", closesZone = 4, position = V(52.0f, 83.0f), yaw = 0.0f },
                new CityLayout.Barrier { name = "Bar_ParkW_Z4", closesZone = 4, position = V(93.0f, 83.0f), yaw = 0.0f },
                new CityLayout.Barrier { name = "Bar_East_Z3", closesZone = 3, position = V(193.0f, 83.0f), yaw = 0.0f },
                new CityLayout.Barrier { name = "Bar_117_Z4", closesZone = 4, position = V(153.0f, 117.0f), yaw = 90.0f },
                new CityLayout.Barrier { name = "Bar_140_Z4", closesZone = 4, position = V(153.0f, 140.0f), yaw = 90.0f },
                new CityLayout.Barrier { name = "Bar_Park_Z4", closesZone = 4, position = V(186.0f, 103.9f), yaw = 77.0f },
            };
        }
    }
}
