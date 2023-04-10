using System.Diagnostics;
using System.Numerics;
using static CS2CheatPOC.Memory;

class CheatClass
{
    static Vector2 WorldToAngle(Vector3 playerPos, Vector3 vector)
    {
        Vector3 relativePos = new Vector3(vector.X - playerPos.X, vector.Y - playerPos.Y, vector.Z - playerPos.Z);
        float yaw = (float) Math.Atan(relativePos.Y / relativePos.X);
        float pitch = (float) -Math.Atan(relativePos.Z / Math.Sqrt(relativePos.X * relativePos.X + relativePos.Y * relativePos.Y));
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

    private static readonly int PROCESS_VM_READ = 0x0010;
    private static readonly int PROCESS_VM_WRITE = 0x0020;
    private static readonly int PROCESS_VM_OPERATION = 0x0008;
    private static readonly int[] playerArrayOffsets = { 0x1472300, 0x120, 0x0, 0x348 };
    private static readonly int pitchOffset = 0x1635694;
    private static readonly int yawOffset = pitchOffset + 0x4;

    static void Main(string[] args)
    {
        string procName = "cs2";
        Console.WriteLine("Starting");
        var process = Process.GetProcessesByName(procName)[0];
        var processHandle = (int)OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);

        long clientDLL = GetModuleBaseAddress(process, "client.dll");
        Console.WriteLine("client.dll: {0:X}", clientDLL);


        long playerArray = GetAddressFromOffsets(processHandle, clientDLL, playerArrayOffsets);
        int playerCount = 30;
        float maxDistance = 2000f;
        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
        {
            Player[] players = ReadStructArray<Player>(processHandle, playerArray, playerCount);
            Player localPlayer = players[0];
            Player? closestEnemy = ClosestEnemy(players);
            if (closestEnemy != null)
                if (DistanceTo(localPlayer, (Player)closestEnemy) <= maxDistance)
                {
                    Vector2 aimAngle = PlayersToAngle(localPlayer, (Player)closestEnemy);
                    WriteFloat(processHandle, clientDLL + pitchOffset, aimAngle.X);
                    WriteFloat(processHandle, clientDLL + yawOffset, aimAngle.Y);
                }
            Thread.Sleep(6);
        }
        CloseHandle(processHandle);
    }
}