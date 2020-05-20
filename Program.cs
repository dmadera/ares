#define DEBUG
#undef DEBUG

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;
using System.Text;
using System.Globalization;

namespace Ares {

    class Program {

        public const string debugOutputfile = @"assets\Ares.txt";
        public const string debugStandardFile = @"assets\response-standard-1.xml";
        public const string debugTaxFile = @"assets\response-dph-1.xml";

        static int Main(string[] args) {
            TextWriter oldOut = Console.Out;

            try {
                string exeDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory.ToString();

                using (var ostrm = new FileStream(exeDir + @"\ares.log", FileMode.Create, FileAccess.Write))
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

                    Company company = AresAPI.getCompanyByIdno(idno);
# if DEBUG
                    company.writeToFile(Program.debugOutputfile);
#else
                    company.writeToFile(exeDir + @"\..\Ares.txt");
#endif
                }
                return 0;

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

        public string serialize() {
            string[] vals = {
                this.idno,
                this.getCreated(),
                this.getName1(),
                this.getName2(),
                this.getStreet(),
                this.city,
                this.zip,
                this.taxpf,
                this.taxno};
            return string.Join(";", vals);
        }

        public void writeToFile(string filename) {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding targetEncoding = Encoding.GetEncoding("iso-8859-2");

            var fs = File.Open(filename, FileMode.Create);
            using (StreamWriter stream = new StreamWriter(fs, targetEncoding)) {
                Console.WriteLine("Writing to file: " + filename);
                string output = this.serialize();
                Console.WriteLine(output);
                stream.WriteLine(output);
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


    static class AresAPI {

        private const string urlFormatStandard = "https://wwwinfo.mfcr.cz/cgi-bin/ares/darv_std.cgi?ico={0}";
        private const string urlFormatTaxno = "https://wwwinfo.mfcr.cz/cgi-bin/ares/ares_es.cgi?obch_jm={0}";

        public static String removeDiacritics(this String s) {
            String normalizedString = s.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < normalizedString.Length; i++) {
                Char c = normalizedString[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static Company getCompanyByIdno(string idno) {

            var doc = new XmlDocument();
#if DEBUG
            doc.Load(Program.debugStandardFile);
#else
            string xmlResponse = AresAPI.makeRequest(AresAPI.urlFormatStandard, idno);
            doc.LoadXml(xmlResponse);
#endif

            XmlNode root = doc.DocumentElement;

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("are", root.Attributes["xmlns:are"].Value);
            nsmgr.AddNamespace("dtt", root.Attributes["xmlns:dtt"].Value);

            var xmlNode = root.SelectSingleNode(".//are:Pocet_zaznamu", nsmgr);

            int count = int.Parse(xmlNode.InnerText);

            Company c = new Company(idno);
            if (count == 0) {
                c.name = "Nenalezeno";
                Console.WriteLine("Not found!");
                return c;
            }

            if (count != 1) {
                throw new Exception("Multiple records found.");
            }

            XmlNode n = null;

            n = root.SelectSingleNode(".//are:Datum_vzniku", nsmgr);
            try {
                string[] aresDate = n.InnerText.Split("-");
                var y = int.Parse(aresDate[0]);
                var m = int.Parse(aresDate[1]);
                var d = int.Parse(aresDate[2]);
                if (n != null) c.created = new DateTime(y, m, d);
            } catch (Exception) { }

            n = root.SelectSingleNode(".//are:Obchodni_firma", nsmgr);
            if (n != null) c.name = n.InnerText;

            n = root.SelectSingleNode(".//dtt:Nazev_obce", nsmgr);
            if (n != null) c.city = n.InnerText;

            n = root.SelectSingleNode(".//dtt:Nazev_ulice", nsmgr);
            if (n != null) c.street = n.InnerText;

            n = root.SelectSingleNode(".//dtt:Cislo_domovni", nsmgr);
            if (n != null) c.houseNumber = n.InnerText;

            n = root.SelectSingleNode(".//dtt:Cislo_orientacni", nsmgr);
            if (n != null) c.locationNumber = n.InnerText;

            n = root.SelectSingleNode(".//dtt:PSC", nsmgr);
            if (n != null) c.zip = n.InnerText;

            c.taxno = AresAPI.getTaxno(idno, c.name, root);

            return c;
        }

        private static string getTaxno(string idno, string name, XmlNode rootPrev) {

            var doc = new XmlDocument();
#if DEBUG
            doc.Load(Program.debugTaxFile);
#else
            string xmlResponse = AresAPI.makeRequest(AresAPI.urlFormatTaxno, removeDiacritics(name));
            doc.LoadXml(xmlResponse);
#endif

            XmlNode root = doc.DocumentElement;
            root.Attributes["xmlns:are"].Value = rootPrev.Attributes["xmlns:are"].Value;
            root.Attributes["xmlns:dtt"].Value = rootPrev.Attributes["xmlns:dtt"].Value;

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("are", root.Attributes["xmlns:are"].Value);
            nsmgr.AddNamespace("dtt", root.Attributes["xmlns:dtt"].Value);

            var xmlNodeList = root.SelectNodes(".//dtt:S", nsmgr);
            foreach (XmlNode node in xmlNodeList) {
                var no = node.SelectSingleNode(".//dtt:ico", nsmgr);
                if (no != null && no.InnerText == idno) {
                    var taxNode = node.SelectSingleNode(".//dtt:p_dph", nsmgr);
                    if (taxNode != null) {
                        return taxNode.InnerText.Replace("dic=", "");
                    }
                }
            }

            Console.WriteLine("Taxno not found.");
            return string.Empty;
        }

        private static string makeRequest(string urlFormat, string param) {
            string xml = string.Empty;
            string uri = String.Format(urlFormat, HttpUtility.UrlEncode(param));

            Console.WriteLine("Making request: " + uri);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream)) {
                xml = reader.ReadToEnd();
            }

            if (String.IsNullOrEmpty(xml)) {
                throw new Exception("Response is empty!");
            }

            return xml;
        }
    }
}
