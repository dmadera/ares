using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Web;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ares {

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

        public static Company getCompanyByIdno(string idno, string testFileName = null, string testFileName1 = null) {

            var doc = new XmlDocument();
            if (testFileName != null) {
                doc.Load(testFileName);
            } else {
                string xmlResponse = AresAPI.makeRequest(AresAPI.urlFormatStandard, idno);
                doc.LoadXml(xmlResponse);
            }

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

            n = root.SelectSingleNode(".//dtt:Cislo_do_adresy", nsmgr);
            if (n != null) c.houseNumber = n.InnerText;

            n = root.SelectSingleNode(".//dtt:PSC", nsmgr);
            if (n != null) c.zip = n.InnerText;

            n = root.SelectSingleNode(".//dtt:Adresa_textem", nsmgr);
            if (n != null) AresAPI.parseTextAddress(c, n.InnerText);

            c.taxno = AresAPI.getTaxno(idno, c.name, root, testFileName1);

            return c;
        }

        private static void parseTextAddress(Company company, string textAddress) {
            var matches = Regex.Matches(textAddress,
                @"^([\p{L}\s]+)([0-9]+)/?([0-9]?)\s([0-9]{3}\s?[0-9]{2})\s?,\s?([\p{L}]+)",
                RegexOptions.Singleline);
            
            if(matches.Count != 1) return;
            
            GroupCollection groups = matches[0].Groups;
            if(groups.Count < 6) return; 
        
            if (string.IsNullOrEmpty(company.houseNumber)) {
                company.houseNumber = groups[2].Value.Trim();
                company.locationNumber = groups[3].Value.Trim();
            }
            if (string.IsNullOrEmpty(company.street)) company.street = groups[1].Value.Trim();
            if (string.IsNullOrEmpty(company.zip)) company.zip = groups[4].Value.Replace(" ", String.Empty).Trim();
            if (string.IsNullOrEmpty(company.city)) company.city = groups[5].Value.Trim();
        }

        private static string getTaxno(string idno, string name, XmlNode rootPrev, string testFileName1) {

            var doc = new XmlDocument();

            if (testFileName1 != null) {
                doc.Load(testFileName1);
            } else {
                string xmlResponse = AresAPI.makeRequest(AresAPI.urlFormatTaxno, removeDiacritics(name));
                doc.LoadXml(xmlResponse);
            }

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