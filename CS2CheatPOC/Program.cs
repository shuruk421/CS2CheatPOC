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

    static Vector2 PlayersToAngle(int processHandle, Player player, Player target)
    {
        int headBoneindex = 6;
        int boneCount = 116;

        CSkeletonInstance playerSkeletonInstance = ReadStruct<CSkeletonInstance>(processHandle, player.m_pGameSceneNode);
        CBoneData[] playerBoneArray = ReadStructArray<CBoneData>(processHandle, playerSkeletonInstance.m_modelState.m_boneArray, boneCount);

        CSkeletonInstance targetSkeletonInstance = ReadStruct<CSkeletonInstance>(processHandle, target.m_pGameSceneNode);
        CBoneData[] targetBoneArray = ReadStructArray<CBoneData>(processHandle, targetSkeletonInstance.m_modelState.m_boneArray, boneCount);

        return WorldToAngle(playerBoneArray[headBoneindex].Location, targetBoneArray[headBoneindex].Location);
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

            if (players[i].m_iTeamNum != player.m_iTeamNum && players[i].m_iHealth != 0)
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

    public static List<Line> GetLines()
    {
        return lines;
    }

    static void UpdateLines(int processHandle, long viewMatrixAddress, Rect window, List<Player> players)
    {
        List<Player> playerList = players.Skip(1).Where(P => P.m_iTeamNum != players.First().m_iTeamNum).ToList();
        var newLines = new List<Line>();
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

            List<Vector3> playerCorners = new List<Vector3>()
            {
                new Vector3(player.XPos - 16, player.YPos - 16, player.ZPos + 72),  //upper corners
                new Vector3(player.XPos - 16, player.YPos + 16, player.ZPos + 72),
                new Vector3(player.XPos + 16, player.YPos + 16, player.ZPos + 72),
                new Vector3(player.XPos + 16, player.YPos - 16, player.ZPos + 72),
                new Vector3(player.XPos - 16, player.YPos - 16, player.ZPos),       //bottom corners
                new Vector3(player.XPos - 16, player.YPos + 16, player.ZPos),
                new Vector3(player.XPos + 16, player.YPos + 16, player.ZPos),
                new Vector3(player.XPos + 16, player.YPos - 16, player.ZPos)
            };

            List<Vector2?> positions = new List<Vector2?>();
            positions = playerCorners.Select(C => WorldToScreen(viewMatrix, C, rect)).ToList();

            if (!positions.Any(P => P == null))
            {
                //top four
                for (int i = 0; i < 3; i++)
                {
                    newLines.Add(new Line()
                    {
                        X1 = (int)positions[i].Value.X,
                        Y1 = (int)positions[i].Value.Y,
                        X2 = (int)positions[i+1].Value.X,
                        Y2 = (int)positions[i+1].Value.Y
                    });
                }
                newLines.Add(new Line()
                {
                    X1 = (int)positions[3].Value.X,
                    Y1 = (int)positions[3].Value.Y,
                    X2 = (int)positions[0].Value.X,
                    Y2 = (int)positions[0].Value.Y
                });


                //bottom four
                for (int i = 4; i < 7; i++)
                {
                    newLines.Add(new Line()
                    {
                        X1 = (int)positions[i].Value.X,
                        Y1 = (int)positions[i].Value.Y,
                        X2 = (int)positions[i + 1].Value.X,
                        Y2 = (int)positions[i + 1].Value.Y
                    });
                }
                newLines.Add(new Line()
                {
                    X1 = (int)positions[7].Value.X,
                    Y1 = (int)positions[7].Value.Y,
                    X2 = (int)positions[4].Value.X,
                    Y2 = (int)positions[4].Value.Y
                });

                //connections
                for (int i = 0; i < 4; i++)
                {
                    newLines.Add(new Line()
                    {
                        X1 = (int)positions[i].Value.X,
                        Y1 = (int)positions[i].Value.Y,
                        X2 = (int)positions[i + 4].Value.X,
                        Y2 = (int)positions[i + 4].Value.Y
                    });
                }
            }
        }
        lines = newLines;
    }

    public static Player[] ReadPlayers(int processHandle, long EntitiesList, int playerCount)
    {
        Player[] players = new Player[playerCount];
        for (int i = 0; i < playerCount + 1; i++)
        {
            long EntAdr = ReadPointer(processHandle, EntitiesList + 0x8 * i);
            if (EntAdr == 0)
                break;
            players[i] = ReadStruct<Player>(processHandle, EntAdr);
        }
        return players;
    }

    private static readonly int PROCESS_VM_READ = 0x0010;
    private static readonly int PROCESS_VM_WRITE = 0x0020;
    private static readonly int PROCESS_VM_OPERATION = 0x0008;
    private static readonly int playerArrayOffset = 0x14A2A48;
    private static readonly int pitchOffset = 0x1635694;
    private static readonly int yawOffset = pitchOffset + 0x4;
    private static readonly int viewMatrixOffset = 0x1627DD0;
    private static List<Line> lines = new List<Line>();
    static void Main(string[] args)
    {
        bool espOn = true;
        bool aimbotOn = true;
        bool fullscreen = false;

        var wssv = new WebSocketServer(8081);

        wssv.AddWebSocketService<ESPBehaviour>("/esp");
        wssv.Start();

        string procName = "cs2";
        Console.WriteLine("Starting");
        var process = Process.GetProcessesByName(procName)[0];
        var processHandle = (int)OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);
        //var windowHandle = process.MainWindowHandle;

        Rect window = new Rect();
        if (!fullscreen)
            //GetWindowRect(windowHandle, ref window);
            window = new Rect() { Top = 0, Left = 0, Bottom = 720, Right = 1280 };
        else
            window = new Rect() { Top = 0, Left = 0, Bottom = 1080, Right = 1920 };

        long clientDLL = GetModuleBaseAddress(process, "client.dll");
        Console.WriteLine("client.dll: {0:X}", clientDLL);

        int playerCount = 30;
        float maxDistance = 2000f;
        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
        {
            Player[] players = ReadPlayers(processHandle, clientDLL + playerArrayOffset, playerCount);
            if (espOn)
                UpdateLines(processHandle, clientDLL + viewMatrixOffset, window,
                    players.Where(P => P.XPos != 0 || P.YPos != 0 || P.ZPos != 0).ToList());
            if (aimbotOn)
            {
                Player localPlayer = players[0];
                Player? closestEnemy = ClosestEnemy(players);
                if (closestEnemy != null)
                    if (DistanceTo(localPlayer, (Player)closestEnemy) <= maxDistance)
                    {
                        Vector2 aimAngle = PlayersToAngle(processHandle, localPlayer, (Player)closestEnemy);
                        WriteFloat(processHandle, clientDLL + pitchOffset, aimAngle.X);
                        WriteFloat(processHandle, clientDLL + yawOffset, aimAngle.Y);
                    }
            }
            Thread.Sleep(1);
        }
        CloseHandle(processHandle);
        wssv.Stop();
    }
}