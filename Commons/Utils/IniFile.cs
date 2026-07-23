using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TaikoSoundEditor.Commons.Utils
{
    public class IniFile
    {
        private readonly string path;
        private readonly string sectionName = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(
            string section,
            string key,
            string value,
            string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string section,
            string key,
            string defaultValue,
            StringBuilder result,
            int size,
            string filePath);

        public IniFile(string iniPath = null)
        {
            path = string.IsNullOrWhiteSpace(iniPath)
                ? Path.Combine(AppContext.BaseDirectory, sectionName + ".ini")
                : Path.GetFullPath(iniPath);
        }

        public string FilePath => path;

        public string Read(string key, string section = null)
        {
            var result = new StringBuilder(1024);
            GetPrivateProfileString(section ?? sectionName, key, string.Empty,
                result, result.Capacity, path);
            return result.ToString();
        }

        public void Write(string key, string value, string section = null)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            if (WritePrivateProfileString(section ?? sectionName, key, value, path) == 0)
                throw new IOException($"Failed to write preference file '{path}'.");
        }

        public void DeleteKey(string key, string section = null) =>
            Write(key, null, section ?? sectionName);

        public void DeleteSection(string section = null) =>
            Write(null, null, section ?? sectionName);

        public bool KeyExists(string key, string section = null) =>
            Read(key, section).Length > 0;
    }
}
