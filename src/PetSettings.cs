using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace HainanTurtlePet
{
    public sealed class PetSettings
    {
        public bool AutoWalk = true;
        public bool AlwaysOnTop = true;
        public int PetSize = 220;
        public int PositionX = Int32.MinValue;
        public int PositionY = Int32.MinValue;

        public static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HainanTurtleDesktopPet", "settings.xml"); }
        }

        public static PetSettings Load(string path)
        {
            try
            {
                XmlReaderSettings xmlSettings = new XmlReaderSettings();
                xmlSettings.DtdProcessing = DtdProcessing.Prohibit;
                xmlSettings.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(path, xmlSettings))
                {
                    PetSettings value = (PetSettings)new XmlSerializer(typeof(PetSettings)).Deserialize(reader);
                    value.PetSize = Math.Max(140, Math.Min(280, value.PetSize));
                    return value;
                }
            }
            catch (IOException) { return new PetSettings(); }
            catch (InvalidOperationException) { return new PetSettings(); }
            catch (XmlException) { return new PetSettings(); }
            catch (UnauthorizedAccessException) { return new PetSettings(); }
        }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                new XmlSerializer(typeof(PetSettings)).Serialize(stream, this);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
