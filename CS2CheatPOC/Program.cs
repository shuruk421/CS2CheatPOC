using CS2CheatPOC;
using CS2CheatPOC.Classes;
using System.Diagnostics;
using System.Numerics;
using WebSocketSharp.Server;
using static CS2CheatPOC.Memory;

namespace CS2CheatPOC;

public class CheatClass
{
    static Vector2 WorldToAngle(Vector3 playerPos, Vector3 vector)
    {
        Vector3 relativePos = new Vector3(vector.X - playerPos.X, vector.Y - playerPos.Y, vector.Z - playerPos.Z);
        float yaw = (float)Math.Atan(relativePos.Y / relativePos.X);
        float pitch = (float)-Math.Atan(relativePos.Z / Math.Sqrt(relativePos.X * relativePos.X + relativePos.Y * relativePos.Y));
        float degrees = 180 / (float)Math.PI;
        yaw *= degrees;
        pitch *= degrees;
        if (relativePos.X < 0)
            yaw = yaw - 180;
        return new Vector2(pitch, yaw);
    }

    static Vector2 PlayersToAngle(Player player, Player target)
    {
        return WorldToAngle(new Vector3(player.XPos, player.YPos, player.ZPos), new Vector3(target.XPos, target.YPos, target.ZPos));
    }

    static float DistanceTo(Player player, Player target)
    {
        return new Vector3(player.XPos - target.XPos, player.YPos - target.YPos, player.ZPos - target.ZPos).Length();
    }

    static Player? ClosestEnemy(Player[] players)
    {
        Player player = players[0];
        Player? closestPlayer = null;
        float minDistance = float.MaxValue;
        for (int i = 1; i < players.Length; i++)
        {
            if (players[i].XPos == 0 && players[i].YPos == 0 && players[i].ZPos == 0)
                continue;

            if (players[i].TeamID != player.TeamID && players[i].Health != 0 && players[i].m_bSpotted == 2)
            {
                var distance = DistanceTo(player, players[i]);
                if (distance < minDistance)
                {
                    closestPlayer = players[i];
                    minDistance = distance;
                }
            }
        }
        return closestPlayer;
    }

    public static Vector2? WorldToScreen(byte[] matrix, Vector3 origin, Rectangle gameWindow)
    {
        float m11 = BitConverter.ToSingle(matrix, 0), m12 = BitConverter.ToSingle(matrix, 16), m13 = BitConverter.ToSingle(matrix, 32), m14 = BitConverter.ToSingle(matrix, 48);
        float m21 = BitConverter.ToSingle(matrix, 4), m22 = BitConverter.ToSingle(matrix, 20), m23 = BitConverter.ToSingle(matrix, 36), m24 = BitConverter.ToSingle(matrix, 52);
        float m31 = BitConverter.ToSingle(matrix, 8), m32 = BitConverter.ToSingle(matrix, 24), m33 = BitConverter.ToSingle(matrix, 40), m34 = BitConverter.ToSingle(matrix, 56);
        float m41 = BitConverter.ToSingle(matrix, 12), m42 = BitConverter.ToSingle(matrix, 28), m43 = BitConverter.ToSingle(matrix, 44), m44 = BitConverter.ToSingle(matrix, 60);

        Vector4 clipCoords;
        clipCoords.X = origin.X * m11 + origin.Y * m21 + origin.Z * m31 + m41;
        clipCoords.Y = origin.X * m12 + origin.Y * m22 + origin.Z * m32 + m42;
        clipCoords.Z = origin.X * m13 + origin.Y * m23 + origin.Z * m33 + m43;
        clipCoords.W = origin.X * m14 + origin.Y * m24 + origin.Z * m34 + m44;

        var screen = new Vector2(0, 0);
        if (clipCoords.W < 0.1f)
            return null;

        Vector3 NDC;
        NDC.X = clipCoords.X / clipCoords.W;
        NDC.Y = clipCoords.Y / clipCoords.W;
        NDC.Z = clipCoords.Z / clipCoords.W;

        screen.X = (gameWindow.Width / 2 * NDC.X) + (NDC.X + gameWindow.Width / 2);
        screen.Y = -(gameWindow.Height / 2 * NDC.Y) + (NDC.Y + gameWindow.Height / 2);
        return screen;
    }

    public static List<Rectangle> GetRectangles()
    {
        return rectangles;
    }

    static void UpdateRectangles(int processHandle, long viewMatrixAddress, Rect window, List<Player> players)
    {
        List<Player> playerList = players.Skip(1).Where(P => P.TeamID != players.First().TeamID).ToList();
        var newRectangles = new List<Rectangle>();
        byte[] viewMatrix = new byte[64];
        int bytesRead = 0;
        ReadProcessMemory(processHandle, viewMatrixAddress, viewMatrix, 64, ref bytesRead);
        foreach (var player in playerList)
        {
            Rectangle rect = new Rectangle()
            {
                X = window.Left,
                Y = window.Top,
                Height = window.Bottom - window.Top,
                Width = window.Right - window.Left
            };
            var playerCorner1 = new Vector3(player.XPos - 16, player.YPos - 16, player.ZPos + 72);
            var playerCorner2 = new Vector3(player.XPos + 16, player.YPos + 16, player.ZPos);
            var pos1 = WorldToScreen(viewMatrix, playerCorner1, rect);
            var pos2 = WorldToScreen(viewMatrix, playerCorner2, rect);
            if (pos1 != null && pos2 != null)
            {
                int x = (int) Math.Min(pos1.Value.X, pos2.Value.X);
                int y = (int) Math.Min(pos1.Value.Y, pos2.Value.Y);
                int width = (int) Math.Max(pos1.Value.X, pos2.Value.X) - x;
                int height = (int) Math.Max(pos1.Value.Y, pos2.Value.Y) - y;
                newRectangles.Add(new Rectangle() { X = x, Y = y, Width = width, Height = height });
            }
        }
        rectangles = newRectangles;
    }

    private static readonly int PROCESS_VM_READ = 0x0010;
    private static readonly int PROCESS_VM_WRITE = 0x0020;
    private static readonly int PROCESS_VM_OPERATION = 0x0008;
    private static readonly int[] playerArrayOffsets = { 0x1472300, 0x120, 0x0, 0x348 };
    private static readonly int pitchOffset = 0x1635694;
    private static readonly int yawOffset = pitchOffset + 0x4;
    private static readonly int viewMatrixOffset = 0x1627DD0;
    private static List<Rectangle> rectangles = new List<Rectangle>();
    static void Main(string[] args)
    {
        bool espOn = true;
        bool aimbotOn = true;
        bool fullscreen = true;

        var wssv = new WebSocketServer(8080);

        wssv.AddWebSocketService<ESPBehaviour>("/esp");
        wssv.Start();

        string procName = "cs2";
        Console.WriteLine("Starting");
        var process = Process.GetProcessesByName(procName)[0];
        var processHandle = (int)OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);
        var windowHandle = process.MainWindowHandle;

        Rect window = new Rect();
        if(!fullscreen)
            GetWindowRect(windowHandle, ref window);
        else
            window = new Rect() { Top= 0, Left= 0, Bottom= 1080, Right= 1920};

        long clientDLL = GetModuleBaseAddress(process, "client.dll");
        Console.WriteLine("client.dll: {0:X}", clientDLL);


        long playerArray = GetAddressFromOffsets(processHandle, clientDLL, playerArrayOffsets);
        int playerCount = 30;
        float maxDistance = 2000f;
        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
        {
            Player[] players = ReadStructArray<Player>(processHandle, playerArray, playerCount);
            if (espOn)
                UpdateRectangles(processHandle, clientDLL + viewMatrixOffset, window,
                    players.Where(P => P.XPos != 0 || P.YPos != 0 || P.ZPos != 0).ToList());
            if (aimbotOn)
            {
                Player localPlayer = players[0];
                Player? closestEnemy = ClosestEnemy(players);
                if (closestEnemy != null)
                    if (DistanceTo(localPlayer, (Player)closestEnemy) <= maxDistance)
                    {
                        Vector2 aimAngle = PlayersToAngle(localPlayer, (Player)closestEnemy);
                        WriteFloat(processHandle, clientDLL + pitchOffset, aimAngle.X);
                        WriteFloat(processHandle, clientDLL + yawOffset, aimAngle.Y);
                    }
            }
            Thread.Sleep(3);
        }
        CloseHandle(processHandle);
        wssv.Stop();
    }
}