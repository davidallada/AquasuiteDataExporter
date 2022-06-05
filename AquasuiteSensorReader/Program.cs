using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

class Program
{
    static void Main(string[] args)
    {
        var fileName = "testexport";
        AquasuiteSharedMemoryExportHelper memory_helper = new AquasuiteSharedMemoryExportHelper(fileName);
        string xmlString = memory_helper.get_xml_string_from_acessor(memory_helper.accessor);
        //Console.WriteLine(xmlString);
        XmlDocument xmlDoc = memory_helper.xml_doc_from_xml_string(xmlString);
        var dict = memory_helper.get_log_dataset_dict_from_xml(xmlDoc);
        var keys = memory_helper.get_data_dict_keys();
    }
}