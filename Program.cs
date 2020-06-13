using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;

namespace Ares {
    enum TaxpayerStatus {
        reliable,
        unreliable,
        notapayer
    }

    class Program {
        public const string debugAssetsFolder = "assets";
        public const string debugStandardFile = "response-standard-5.xml";
        public const string debugTaxFile = "response-dph-1.xml";
        public const string debugTaxStatusFile = "response-mfcr-3.xml";

        static int Main(string[] args) {
            TextWriter oldOut = Console.Out;

            try {
                string exeDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory.ToString();

                using (var ostrm = new FileStream(Path.Combine(exeDir, "ares.log"), FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(ostrm)) {
#if !DEBUG
                    Console.SetOut(writer);
#endif
                    Console.WriteLine("Exe DIR: " + exeDir);

                    if (args.Length != 1) {
                        throw new ArgumentException("Ivalid aguments count.");
                    }

                    string idno = Company.formatIco(args[0].Replace(" ", ""));
                    Console.WriteLine(string.Format("Passed ID: {0}", idno));
#if DEBUG
                    Company company = AresAPI.getCompanyByIdno(idno, Path.Combine(debugAssetsFolder, debugStandardFile), Path.Combine(debugAssetsFolder, debugTaxFile));
                    company.taxpayerStatus = MfcrAPI.getTaxpayerStatus(company.taxno, Path.Combine(debugAssetsFolder, debugTaxStatusFile));
                    company.writeToFile(Path.Combine(exeDir, "Ares.txt"));
#else
                    Company company = AresAPI.getCompanyByIdno(idno);
                    company.taxpayerStatus = MfcrAPI.getTaxpayerStatus(company.taxno);
                    company.writeToFile(Path.Combine(exeDir, "..", "Ares.txt"));
#endif
                }
                return 1;

            } catch (Exception e) {
                Console.WriteLine(e.Message);
                return -1;

            } finally {
                Console.SetOut(oldOut);
            }
        }
    }

    class Company {
        public string name = "";
        protected string idno = "";
        public string taxpf = "CZ";
        public string taxno = "";
        public string street = "";
        public string houseNumber = "";
        public string locationNumber = "";
        public string city = "";
        public string zip = "";
        public TaxpayerStatus taxpayerStatus = TaxpayerStatus.notapayer;

        public DateTime created = new DateTime(0);

        public Company(string idno) {
            this.idno = idno;
        }

        public string getName1() {
            if (this.name.Length <= 35) {
                return this.name;
            } else {
                int iSpace = -1;
                string n = this.name;
                do {
                    iSpace = n.LastIndexOf(" ");
                    n = (iSpace < 0 ? n : n.Substring(0, iSpace));
                } while (iSpace >= 35);

                if (iSpace < 0) {
                    iSpace = 35;
                }
                return this.name.Substring(0, iSpace);
            }
        }

        public string getName2() {
            if (this.name.Length <= 35) {
                return string.Empty;
            } else {
                int iSpace = -1;
                string n = this.name;
                do {
                    iSpace = n.LastIndexOf(" ");
                    n = (iSpace < 0 ? n : n.Substring(0, iSpace));
                } while (iSpace >= 35);

                if (iSpace < 0) {
                    iSpace = 35;
                }
                return this.name.Substring(iSpace).TrimStart();
            }
        }

        public string getStreet() {
            string streetFormat = "{0} {1}/{2}";
            if (this.locationNumber == "") {
                streetFormat = "{0} {1}";
            }

            return string.Format(streetFormat,
                this.street, this.houseNumber, this.locationNumber);
        }

        public string getCreated() {
            if (this.created.Ticks == 0) return "";
            return this.created.ToString("dd.MM.yy");
        }

        public string getTaxpf() {
            if (this.taxpayerStatus == TaxpayerStatus.notapayer) return "";
            return this.taxpf;
        }

        public string getTaxno() {
            if (this.taxpayerStatus == TaxpayerStatus.notapayer) return "";
            return this.taxno;
        }

        public string getTaxpayerStatus() {
            switch (this.taxpayerStatus) {
                case TaxpayerStatus.notapayer: return "neplatce";
                case TaxpayerStatus.reliable: return "spolehlivy";
                case TaxpayerStatus.unreliable: return "nespolehlivy";
                default: return "";
            }
        }

        public string[] getLines() {
            string[] vals = {
                this.idno,
                this.getCreated(),
                this.getName1(),
                this.getName2(),
                this.getStreet(),
                this.city,
                this.zip,
                this.getTaxpf(),
                this.getTaxno(),
                this.getTaxpayerStatus()};
            return vals;
        }

        public void writeToFile(string filename) {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding targetEncoding = Encoding.GetEncoding("ibm852");

            var fs = File.Open(filename, FileMode.Create);
            using (StreamWriter stream = new StreamWriter(fs, targetEncoding)) {
                Console.WriteLine("Writing to file: " + filename);
                string[] lines = this.getLines();
                for (int i = 0; i < lines.Length; i++) {
                    Console.WriteLine(lines[i]);
                    stream.WriteLine(lines[i]);
                }
            }
        }

        public static string formatIco(string idno) {
            idno = idno.PadLeft(8, '0');
            if (!Company.isValidIdno(idno)) {
                throw new Exception("ICO is invalid.");
            }
            return idno;
        }

        private static bool isValidIdno(string idno) {
            Regex rgx = new Regex(@"^\d{8}$");

            if (!rgx.IsMatch(idno)) {
                return false;
            }

            char[] id = idno.ToCharArray();

            int a = 0, c = 0, i;
            for (i = 0; i < 7; i++) {
                a += int.Parse(idno[i].ToString()) * (8 - i);
            }

            a = a % 11;
            if (a == 0) {
                c = 1;
            } else if (a == 1) {
                c = 0;
            } else {
                c = 11 - a;
            }

            return int.Parse(idno[7].ToString()) == c;
        }
    }
}
