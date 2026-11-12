using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DrugUsageSystem.Data
{
    /// <summary>
    /// Минимальный генератор XLSX без внешних пакетов:
    /// XLSX — это ZIP-архив с XML-файлами, собираем встроенными средствами .NET.
    /// Шапка жирная, ширина колонок подбирается по содержимому.
    /// </summary>
    public static class MiniXlsx
    {
        public static void Write(string path, string sheetName,
                                 IList<string> header, IList<IList<string>> rows)
        {
            if (File.Exists(path))
                File.Delete(path);

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");

                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                AddEntry(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"" + Esc(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

                AddEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");

                // Стиль 0 — обычный, стиль 1 — жирный (шапка)
                AddEntry(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<fonts count=\"2\">" +
                    "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                    "</fonts>" +
                    "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                    "<borders count=\"1\"><border/></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"2\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                    "</cellXfs>" +
                    "</styleSheet>");

                AddEntry(zip, "xl/worksheets/sheet1.xml", BuildSheet(header, rows));
            }
        }

        private static string BuildSheet(IList<string> header, IList<IList<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            // Ширина колонок по самой длинной строке (с ограничениями)
            sb.Append("<cols>");
            for (int c = 0; c < header.Count; c++)
            {
                int maxLen = header[c].Length;
                foreach (var row in rows)
                    if (c < row.Count && row[c] != null && row[c].Length > maxLen)
                        maxLen = row[c].Length;
                double width = Math.Min(60, Math.Max(10, maxLen * 1.15 + 2));
                sb.Append("<col min=\"").Append(c + 1).Append("\" max=\"").Append(c + 1)
                  .Append("\" width=\"").Append(width.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                  .Append("\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");

            sb.Append("<sheetData>");
            AppendRow(sb, 1, header, bold: true);
            int r = 2;
            foreach (var row in rows)
                AppendRow(sb, r++, row, bold: false);
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, int rowIndex, IList<string> cells, bool bold)
        {
            sb.Append("<row r=\"").Append(rowIndex).Append("\">");
            for (int c = 0; c < cells.Count; c++)
            {
                string cellRef = ColLetter(c) + rowIndex;
                sb.Append("<c r=\"").Append(cellRef).Append("\"");
                if (bold)
                    sb.Append(" s=\"1\"");
                sb.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                  .Append(Esc(cells[c] ?? ""))
                  .Append("</t></is></c>");
            }
            sb.Append("</row>");
        }

        private static string ColLetter(int index)
        {
            // 0 -> A, 25 -> Z, 26 -> AA ...
            string s = "";
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                s = (char)('A' + rem) + s;
                index = (index - 1) / 26;
            }
            return s;
        }

        private static string Esc(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }
    }
}
