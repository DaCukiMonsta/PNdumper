using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PNdumper
{
    class Program
    {

        const string privacy_notice = "Your provided information may be stored for research purposes, but will not be tied to you individually.\n"+
                                      "Your console serial numbers will not be published under any circumstance, but may be used to privately\n" + 
                                      "research serial number formats. Other anonymised information such as your console part number (which is\n"+
                                      "not unique) may be published as part of a public database of 360 part numbers.\n"+
                                      "Before your results file is created, you will be given the option to censor/redact all serial numbers.\n"+
                                      "Once your anonymised information is sent to us, we may not be able to delete it once it is added to the database.";
        static string csv = "error creating csv file!";
        enum MotherboardType : int
        {
            Xenon = 1,
            Zephyr = 2,
            FalconOpus = 3,
            Jasper = 4,
            Trinity = 5,
            Corona = 6,
            Winchester = 7
        }

        enum ConsoleRegion : int
        {
            PAL_EUROPE = 0x2FE,
            NTSC_USA = 0xFF,
            NTSC_JAPAN1 = 0x1FE,
            NTSC_JAPAN2 = 0x1FF,
            NTSC_KOREA = 0x1FC,
            NTSC_HONGKONG = 0x101,
            PAL_AUSTRALIA = 0x201,
            DEVKIT = 0x7FFF
        }

        enum NANDSize : int
        {
            _16MB = 16777216, //17301504,
            _256_512MB = 67108864, //69206016,
            _256MB = 268435456, //276824064,
            _512MB = 536870912, //553648128,
            _4GB = -1
        }

        static Dictionary<String, Dictionary<String, Object>> DB = new Dictionary<String, Dictionary<String, Object>> { };

        static void Main(string[] args)
        {
            Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()+  " by DaCukiMonsta");
            Console.WriteLine();

            Console.WriteLine("PLEASE READ THE FOLLOWING");
            Console.WriteLine("=========================");
            Console.WriteLine("Thank you for considering contributing to our project. This tool collects information about the consoles\n"+
                              "you have NAND dumps for. This includes the console part/product number.\n" +
                              "We will use the part number in combination with other information to compile a public database of Xbox 360\n" +
                              "part/product numbers. We believe the part number represents the specific retail configuration/SKU of your\n" +
                              "console at purchase. It is not personally idenitifiable or unique to you.\n");
            Console.WriteLine("A console part number is of the form Xnnnnnn-001 where n can be any digit. However, this can only be extracted\n" +
                              "from your NAND. A similar part number appears on the console motherboard itself, however this is NOT the part\n" +
                              "number we are looking for at this time, it is always different and that the part number of the PCB itself.");
            Console.WriteLine("\nThis application will compile a CSV file for you to manually email to us. This application does not connect\n" +
                              "to the internet, and does not send any data to us unless you specifically decide to email the resulting file\n" +
                              "to us yourself.");
            Console.WriteLine();
            Console.WriteLine(privacy_notice);
            Console.WriteLine();

            if (!yes_or_no("Do you understand and agree to the above?"))
            {
                Console.WriteLine("No problem. If you have any questions or concerns, please contact us at 360partnumbers@gmail.com.");
                Console.WriteLine("Thank you for considering to help, anyway. :)");
                pakte();
            }

            string search_path;
            if(args.Length == 1)
            {

                search_path = args[0];
                if (!yes_or_no("Using "+search_path + " as NAND search path. Okay?", true))
                {
                    Console.WriteLine("Cannot continue, please try again.");
                    pakte();
                }
            }
            else
            {
                search_path = Directory.GetCurrentDirectory();
                if (!yes_or_no("Using current directory " + search_path + " as NAND search path. Okay?", true))
                {
                    Console.WriteLine("Cannot continue, please try again.");
                    pakte();
                }
            }

            Console.WriteLine("Reading registry for JRunner console database information...");
            read_DB();

            Console.WriteLine("Searching for NANDs...");
            try
            {
                Dictionary<string, string> nands = scan_for_nands(search_path);
                Console.WriteLine("Found " + nands.Count + " potential NANDs.");
                if(nands.Count == 0)
                {
                    Console.WriteLine("No NANDs could be found. Each NAND should be in a separate folder, with a nanddump1.bin file and a cpukey.txt file.");
                    pakte();
                }
                List<Dictionary<string, string>> results = process_nands(nands);
                Console.WriteLine("Creating CSV file. The file will only contain information shown to you already above and nothing more.");
                bool hide_serials = yes_or_no("Would you like to censor/redact the console serial numbers from the resulting CSV file?", false);
                create_csv(results, hide_serials);
                string save_path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\pndumper-" + DateTime.Now.ToString("ddMMyyHHmmss") + ".csv";
                Console.WriteLine("Saving resulting CSV file to " + save_path + "...");
                File.WriteAllText(save_path, csv);
                Console.WriteLine("Done. Please send your CSV file to 360partnumbers@gmail.com. You may need to press F5 on your desktop to see it.");
                Console.WriteLine("If you have any additional information that could help up about any of these specific consoles (original retail \n" +
                    "configuration information, such as original case colour, hard drive size originally sold with, or motherboard version (v1-v6 etc)\n" +
                    "for corona motherboards), we would appreciate this too. Please add this to your email along with which console it applies to.");
                Console.WriteLine("Thank you for your contribution :)");
            }
            catch(Exception ex){
                Console.WriteLine();
                Console.WriteLine("Fatal Error, cannot continue. Error: " + ex.Message);
            }
            pakte();
        }

        static void pakte()
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        static bool yes_or_no(string prompt)
        {
            Console.Write(prompt + " (y/n):");
            string read = Console.ReadLine();
            while (read != "y" && read != "n")
            {
                Console.WriteLine("Please enter only y or n.");
                Console.Write(prompt + " (y/n):");
                read = Console.ReadLine();
            }
            return (read == "y");
        }
        static bool yes_or_no(string prompt, bool default_)
        {
            Console.Write(prompt + " (y/n, default "+(default_ ? "y": "n")+"):");
            string read = Console.ReadLine();
            while (read != "y" && read != "n" && read != "")
            {
                Console.WriteLine("Please enter only y or n.");
                Console.Write(prompt + " (y/n, default " + (default_ ? "y" : "n") + "):");
                read = Console.ReadLine();
            }
            if(read == "")
            {
                return default_;
            }
            return (read == "y");
        }

        static void create_csv(List<Dictionary<string, string>> results, bool HideSerials = false)
        {
            csv = "serial, partnumber, mfrdate, mobo, smc_ver, dvd, region\n";
            foreach (Dictionary<string, string> result in results)
            {
                if (!HideSerials)
                {
                    csv += result["serial"] + ", ";
                }
                else
                {
                    csv += "--REDACTED--, ";
                }
                csv += result["p/n"] + ", " + result["mfr-date"] + ", " + result["mbr"] + ", " + result["smcver"] + ", ";
                if (result.ContainsKey("dvd"))
                {
                    csv += result["dvd"] + ", ";
                }
                else
                {
                    csv += "?, ";
                }
                if (result.ContainsKey("region"))
                {
                    csv += result["region"] + "\n";
                }
                else
                {
                    csv += "?\n";
                }
            }
        }

        static void read_DB()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("CPUKey_DB");
            if(key != null)
            {
                foreach (string v in key.GetSubKeyNames())
                {
                    RegistryKey console = key.OpenSubKey(v);
                    if (console != null)
                    {
                        string Serial;
                        Dictionary<String, Object> thisConsole = new Dictionary<String, Object> { };
                        if (console.GetValue("Serial") == null || console.GetValue("Mobo") == null || console.GetValue("OSIG") == null || console.GetValue("Region") == null)
                        {
                            continue;
                        }
                        Serial = (string)console.GetValue("Serial");
                        string mobo = (string)console.GetValue("Mobo");
                        switch (mobo)
                        {
                            case "Falcon/Opus":
                                mobo = "FalconOpus";
                                break;
                            case "Jasper BB":
                                mobo = "Jasper 256/512MB";
                                break;
                        }
                        thisConsole.Add("Mobo", mobo);
                        thisConsole.Add("DVD", ((string)console.GetValue("OSIG")).Split(new char[0], StringSplitOptions.RemoveEmptyEntries)[1]);
                        thisConsole.Add("Region", (ConsoleRegion)(int.Parse((string)console.GetValue("Region"), System.Globalization.NumberStyles.HexNumber)));
                        DB.Add(Serial, thisConsole);
                    }
                }
            }
        }

        static Dictionary<string, string> scan_for_nands(string root)
        {
            Console.WriteLine("Scanning for NANDs in \"" + root + "\"...");
            Dictionary<string, string> results = new Dictionary<string, string> { }; // key = nanddump1.bin, value = cpukey.txt
            IEnumerable<string> files = Directory.EnumerateFiles(root, "nanddump1.bin", SearchOption.AllDirectories);
            foreach (string filepath in files)
            {
                FileInfo fileInfo = new FileInfo(filepath);
                string directoryFullPath = fileInfo.DirectoryName;
                string[] parts = directoryFullPath.Split(Path.DirectorySeparatorChar);
                Console.Write("Found nanddump1.bin in folder " + parts[parts.Length - 1] + " ");
                string cpukeyfilepath = Path.Combine(directoryFullPath, "cpukey.txt");
                if (File.Exists(cpukeyfilepath))
                {
                    Console.WriteLine("with a cpukey.txt");
                    results.Add(filepath, cpukeyfilepath);
                }
                else
                {
                    Console.WriteLine("without a cpukey.txt!");
                }
            }
            return results;
        }

        static List<Dictionary<string, string>> process_nands(Dictionary<string, string> nands)
        {
            Console.WriteLine("Getting part numbers, this may take 5-30 seconds per NAND...");
            List<Dictionary<string, string>> results = new List<Dictionary<string, string>>{ };
            foreach (string nand_path in nands.Keys)
            {
                string cpukey_path = nands[nand_path];
                byte[] nand = File.ReadAllBytes(nand_path);
                string cpukey = File.ReadAllText(cpukey_path);
                Dictionary<string, string> result = get_part_number(nand, cpukey);
                Console.Write("Done. Serial: "+result["serial"]+" P/N: "+ result["p/n"] + " MFR DATE: " + result["mfr-date"] + " MBR: "+result["mbr"] + " SMC_VER: " + result["smcver"]);

                if (result.ContainsKey("dvd"))
                {
                    Console.Write(" DVD: " + result["dvd"]);
                }
                if (result.ContainsKey("region"))
                {
                    Console.Write(" REGION: " + result["region"]);
                }
                Console.WriteLine();
                results.Add(result);
            }
            return results;
        }

        static string get_motherboard_nand_string(MotherboardType MbType, int NANDSizebytes)
        {
            string result = MbType.ToString();
            if (MbType == MotherboardType.Jasper || MbType == MotherboardType.Corona) {
                result += " ";
                switch ((NANDSize)NANDSizebytes)
                {
                    case NANDSize._16MB:
                        result += "16MB";
                        break;
                    case NANDSize._256_512MB:
                        result += "256/512MB";
                        break;
                    case NANDSize._256MB:
                        result += "256MB";
                        break;
                    case NANDSize._512MB:
                        result += "512MB";
                        break;
                    default:
                        result += "4GB";
                        break;
                }
            }
            return result;
        }

        static Dictionary<string, string> get_part_number(byte[] nand, string cpukey)
        {
            Dictionary<string, string> results = new Dictionary<string, string> { };
            if (String.IsNullOrWhiteSpace(cpukey)) throw new Exception("Bad CPUkey");
            byte[] key = StringToByteArray(cpukey);

            if (hasecc_v2(ref nand)) nand = unecc(ref nand, false);

            byte[] Keyvault = new byte[0x4000];
            Buffer.BlockCopy(nand, 0x4000, Keyvault, 0, 0x4000);
            byte[] kv_dec = decryptkv(Keyvault, key);
            if (!allsame(returnportion(kv_dec, 0x40, 0x20), 0x00))
            {
                throw new Exception("Bad CPUkey");
            }

            // decrypt SMC to get MB type
            byte[] SMC = null;
            byte[] smc_len = new byte[4], smc_start = new byte[4];
            Buffer.BlockCopy(nand, 0x78, smc_len, 0, 4);
            Buffer.BlockCopy(nand, 0x7C, smc_start, 0, 4);
            SMC = new byte[ByteArrayToInt(smc_len)];
            Buffer.BlockCopy(nand, ByteArrayToInt(smc_start), SMC, 0, ByteArrayToInt(smc_len));
            SMC = decrypt_SMC(SMC);

            byte[] pn = new byte[0xB];
            Buffer.BlockCopy(kv_dec, 0x9CF, pn, 0, 0xB);

            byte[] mfr_date = new byte[0x8];
            Buffer.BlockCopy(kv_dec, 0x9E4, mfr_date, 0, 0x8);

            byte[] serial = new byte[0xC];
            Buffer.BlockCopy(kv_dec, 0xB0, serial, 0, 0xC);

            results.Add("p/n", System.Text.Encoding.UTF8.GetString(pn));
            results.Add("mfr-date", System.Text.Encoding.UTF8.GetString(mfr_date));
            results.Add("serial", System.Text.Encoding.UTF8.GetString(serial));
            results.Add("smcver", SMC[0x101].ToString() + "." + SMC[0x102].ToString());
            if (DB.ContainsKey(results["serial"])){
                results.Add("mbr", (string)DB[results["serial"]]["Mobo"]);
                results.Add("dvd", (string)DB[results["serial"]]["DVD"]);
                results.Add("region", ((ConsoleRegion)DB[results["serial"]]["Region"]).ToString());
            } else
            {
                byte[] region_bytes = new byte[0x2];
                Buffer.BlockCopy(kv_dec, 0xC8, region_bytes, 0, 0x2);

                byte[] dvd_bytes = new byte[0x8];
                Buffer.BlockCopy(kv_dec, 0xC9A, dvd_bytes, 0, 0x8);

                ConsoleRegion region = (ConsoleRegion)((region_bytes[0] << 8) + region_bytes[1]);

                MotherboardType MbType = (MotherboardType)(SMC[0x100] >> 4 & 0xF);
                string MbNANDName = get_motherboard_nand_string(MbType, nand.Length);
                results.Add("mbr", MbNANDName);
                results.Add("region", region.ToString());
                results.Add("dvd", System.Text.Encoding.UTF8.GetString(dvd_bytes));
            }

            return results;
        }

        public static byte[] decrypt_SMC(byte[] SMC)
        {

            byte[] Key = { 0x42, 0x75, 0x4e, 0x79 };
            int[] Keys = { 0x42, 0x75, 0x4E, 0x79 };
            int i = 0;
            int mod;
            byte[] res = new byte[SMC.Length];
            for (i = 0; i < SMC.Length; i++)
            {
                mod = (SMC[i] * 0xFB);
                res[i] = (byte)(SMC[i] ^ (Keys[i & 3] & 0xFF));
                Keys[(i + 1) & 3] += mod;
                Keys[(i + 2) & 3] += (mod >> 8);
            }
            return res;
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            if (NumberChars % 2 != 0)
            {
                hex = "0" + hex;
                NumberChars++;
            }
            if (NumberChars % 4 != 0)
            {
                hex = "00" + hex;
                NumberChars += 2;
            }
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static byte[] decryptkv(byte[] kv, byte[] key)
        {
            try
            {
                if (kv == null || key == null) return null;
                byte[] message = new byte[16];
                message = returnportion(kv, 0, 0x10);
                byte[] RC4_key = HMAC_SHA1(key, message);
                if (RC4_key == null) return null;
                byte[] restofkv = returnportion(kv, 0x10, kv.Length - 0x10);
                RC4_v(ref restofkv, returnportion(RC4_key, 0, 0x10));
                byte[] finalimage = new byte[message.Length + restofkv.Length];
                for (int i = 0; i < message.Length + restofkv.Length; i++)
                {
                    if (i < message.Length) finalimage[i] = message[i];
                    else finalimage[i] = restofkv[i - message.Length];
                }
                return finalimage;
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (Exception ex) {}
#pragma warning restore CS0168 // Variable is declared but never used
            return null;
        }

        public static byte[] returnportion(byte[] inbytes, int start, int len)
        {
            Byte[] bytes = new Byte[len];
            Buffer.BlockCopy(inbytes, start, bytes, 0, len);
            return bytes;
        }

        public static bool allsame(byte[] inbytes, byte value)
        {
            foreach(byte byt in inbytes)
            {
                if(byt != value)
                {
                    return false;
                }
            }
            return true;
        }

        public static byte[] HMAC_SHA1(byte[] Key, byte[] Message)
        {
            if (Key.Length < 0x10) return null;
            byte[] K = new byte[0x40];
            byte[] opad = new byte[20 + 0x40];
            byte[] ipad = new byte[Message.Length + 0x40];

            Array.Copy(Key, K, 16);

            for (int i = 0; i < 64; i++)
            {
                opad[i] = (byte)(K[i] ^ 0x5C);
                ipad[i] = (byte)(K[i] ^ 0x36);
            }

            // Copy Buffer
            Array.Copy(Message, 0, ipad, 0x40, Message.Length);

            // Get First Hash
            SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
            byte[] Hash1 = sha.ComputeHash(ipad);

            // Copy to OPad
            Array.Copy(Hash1, 0, opad, 0x40, 20);

            return sha.ComputeHash(opad);
        }

        public static void RC4_v(ref Byte[] bytes, Byte[] key)
        {
            Byte[] s = new Byte[256];
            Byte[] k = new Byte[256];
            Byte temp;
            int i, j;

            for (i = 0; i < 256; i++)
            {
                s[i] = (Byte)i;
                k[i] = key[i % key.GetLength(0)];
            }

            j = 0;
            for (i = 0; i < 256; i++)
            {
                j = (j + s[i] + k[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }

            i = j = 0;
            for (int x = 0; x < bytes.GetLength(0); x++)
            {
                i = (i + 1) % 256;
                j = (j + s[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
                int t = (s[i] + s[j]) % 256;
                bytes[x] ^= s[t];
            }
        }

        public static bool hasecc(ref byte[] data)
        {
            int i = 0x200, counter = 0;
            while (i < data.Length && counter <= 0x100)
            {
                byte[] sparedata = new byte[0x40];

                switch (i % 800)
                {
                    case 0:
                        Buffer.BlockCopy(data, i, sparedata, 0, 0x40);
                        i += 0x40;
                        if (sparedata[0] == 0xFF && sparedata[0x10] == 0xFF &&
                            sparedata[0x20] == 0xFF && sparedata[0x30] == 0xFF &&
                            !allsame(sparedata, 0xFF) && sparedata[3] == 0x00 && sparedata[4] == 0x00)
                        {
                            return true;
                        }
                        break;
                    default:
                        Buffer.BlockCopy(data, i, sparedata, 0, 0x10);
                        i += 0x10;
                        if ((sparedata[0] == 0xFF || sparedata[5] == 0xFF) && !allsame(returnportion(sparedata, 0xC, 0x4), 0xFF)
                            && !allsame(returnportion(sparedata, 0xC, 0x4), 0x00) && sparedata[3] == 0x00 && sparedata[4] == 0x00)
                        {
                            return true;
                        }
                        break;
                }
                i += 0x200;
                if (i % 4200 == 0) counter++;
            }
            return false;
        }

        public static bool hasecc_v2(ref byte[] data)
        {
            int block_offset_b = Convert.ToInt32(ByteArrayToString(returnportion(data, 0x8, 4)), 16);
            if (data.Length < block_offset_b + 2) return hasecc(ref data);
            else
            {
                if (data[block_offset_b] == 0x43 && data[block_offset_b + 1] == 0x42)
                {
                    int length = Convert.ToInt32(ByteArrayToString(returnportion(data, block_offset_b + 0xC, 4)), 16);
                    if (data.Length < block_offset_b + length || length < 0) return hasecc(ref data);
                    else
                    {
                        block_offset_b = block_offset_b + length;
                        if (data[block_offset_b] == 0x43 && (data[block_offset_b + 1] == 0x42 || data[block_offset_b + 1] == 0x44)) return false;
                        else return true;
                    }
                }
                else return true;
            }
        }

        public static byte[] unecc(ref byte[] data, bool print = false)
        {
            int counter = 0;
            try
            {
                if (data[0x205] == 0xFF || data[0x415] == 0xFF || data[0x200] == 0xFF)
                {
                    byte[] res = new byte[(data.Length / 0x210) * 0x200];
                    for (counter = 0; counter < res.Length; counter += 0x200)
                    {
                        if (((counter / 0x200) * 0x210) + 0x200 <= data.Length) Buffer.BlockCopy(data, (counter / 0x200) * 0x210, res, counter, 0x200);
                    }
                    data = res;
                    res = null;
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (Exception ex) { }
#pragma warning restore CS0168 // Variable is declared but never used

            return data;
        }

        public static string ByteArrayToString(byte[] ba, int startindex = 0, int length = 0)
        {
            if (ba == null) return "";
            string hex = BitConverter.ToString(ba);
            if (startindex == 0 && length == 0) hex = BitConverter.ToString(ba);
            else if (length == 0 && startindex != 0) hex = BitConverter.ToString(ba, startindex);
            else hex = BitConverter.ToString(ba, startindex, length);
            return hex.Replace("-", "");
        }

        public static int ByteArrayToInt(byte[] value)
        {
            return Convert.ToInt32(ByteArrayToString(value), 16);
        }

    }
}
