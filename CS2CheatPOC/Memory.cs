using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CS2CheatPOC
{
    public class Memory
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(int hObject);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int nSize, ref int lpNumberOfBytesWritten);

        [DllImport("psapi.dll")]
        public static extern bool EnumProcessModulesEx(int hProcess, long[] lphModule, long cb, ref int lpcbNeeded, int dwFilterFlag);

        [DllImport("psapi.dll")]
        public static extern bool GetModuleFileNameExA(int hProcess, long hModule, char[] lpFilename, int nSize);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        public static long GetModuleBaseAddress(Process process, string moduleName)
        {

            // Get an instance of the specified module in the process
            // We use linq here to avoid unnecesary for loops

            var module = process.Modules.Cast<ProcessModule>().SingleOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));

            // Attempt to get the base address of the module - Return IntPtr.Zero if the module doesn't exist in the process
            return (long) module?.BaseAddress;
        }

        public static int WriteFloat(int processHandle, long address, float value)
        {
            int bytesWritten = 0;
            byte[] bytes = BitConverter.GetBytes(value);
            WriteProcessMemory(processHandle, address, bytes, sizeof(float), ref bytesWritten);
            return bytesWritten;
        }

        public static int ReadInt(int processHandle, long address)
        {
            byte[] buff = new byte[4];
            int bytesRead = 0;
            bool readMemory = ReadProcessMemory(processHandle, address, buff, buff.Length, ref bytesRead);
            if (!readMemory)
                throw new Exception("Error reading int");
            return BitConverter.ToInt32(buff);
        }

        public static long ReadPointer(int processHandle, long address)
        {
            byte[] buff = new byte[8];
            int bytesRead = 0;
            bool readMemory = ReadProcessMemory(processHandle, address, buff, buff.Length, ref bytesRead);
            if (!readMemory)
                throw new Exception("Error reading int");
            return BitConverter.ToInt64(buff);
        }
        public static long GetAddressFromOffsets(int processHandle, long baseAddress, int[] offsets)
        {
            long lastAddress = baseAddress;
            foreach (var offset in offsets.Take(offsets.Length - 1))
            {
                lastAddress = ReadPointer(processHandle, lastAddress + offset);
            }
            return lastAddress + offsets.Last();
        }

        public static unsafe T[] ReadStructArray<T>(int processHandle, long address, int length) where T : struct
        {
            T[] array = new T[length];
            for (int i = 0; i < length; i++)
            {
                unsafe
                {
                    array[i] = ReadStruct<T>(processHandle, address + sizeof(T) * i);
                }
            }
            return array;
        }

        public static unsafe T ReadStruct<T>(int processHandle, long address) where T : struct
        {
            unsafe
            {
                T result = new T();
                byte[] buff = new byte[sizeof(T)];
                int bytesRead = 0;
                bool readMemory = ReadProcessMemory(processHandle, address, buff, buff.Length, ref bytesRead);
                if (!readMemory)
                    throw new Exception("Error reading struct");
                fixed (byte* bufferPtr = buff)
                {
                    Buffer.MemoryCopy(bufferPtr, &result, sizeof(T), sizeof(T));
                }
                return result;
            }
        }
    }
}
