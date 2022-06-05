using System;
using System.IO.MemoryMappedFiles;
using System.Text.RegularExpressions;
using System.Xml;

public class AquasuiteSharedMemoryExportHelper
{
    private string filename;
    public MemoryMappedFile mmapped_file;
    public MemoryMappedViewAccessor accessor;
    public Dictionary<string, LogDataSet> data_dict;

    public class LogDataSet
    {
        public string time;
        public string value;
        public string name;
        public string unit;
        public string valueType;
        public string device;

        public LogDataSet(XmlNode xNode)
        {
            time = xNode.SelectSingleNode("./t").InnerText;
            value = xNode.SelectSingleNode("./value").InnerText;
            name = xNode.SelectSingleNode("./name").InnerText;
            unit = xNode.SelectSingleNode("./unit").InnerText;
            valueType = xNode.SelectSingleNode("./valueType").InnerText;
            device = xNode.SelectSingleNode("./device").InnerText;
        }

        public Dictionary<string, string> ToDict()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary.Add("time", time);
            dictionary.Add("value", value);
            dictionary.Add("name", name);
            dictionary.Add("unit", unit);
            dictionary.Add("valueType", valueType);
            dictionary.Add("device", device);

            return dictionary;
        }

        public override string ToString()
        {
            string ret_str = device + "/" + name + "\n";
            foreach (var kvp in ToDict())
            {
                ret_str += String.Format("\t{0}: {1}\n", kvp.Key, kvp.Value);
            }
            return ret_str;
        }
    }
    public AquasuiteSharedMemoryExportHelper(string in_filename)
    {
        filename = in_filename;
        mmapped_file = mmapped_file_from_filename(in_filename);
        accessor = accessor_from_mmapped_file(mmapped_file);
        update_data_dict();
    }

    public MemoryMappedFile mmapped_file_from_filename(string in_filename)
    {
        return MemoryMappedFile.OpenExisting(in_filename);
    }

    public MemoryMappedViewAccessor accessor_from_mmapped_file(MemoryMappedFile mmap_file)
    {
        return mmap_file.CreateViewAccessor();
    }

    public string get_xml_string_from_acessor(MemoryMappedViewAccessor accessor)
    {
        var length = (int)accessor.Capacity;
        var rawBytes = new byte[length];

        accessor.ReadArray(0, rawBytes, 0, length);
        var byte_str = System.Text.UTF8Encoding.UTF8.GetString(rawBytes);

        //For some reason, the header starts with garbled bytes. Not sure why. Remove all of them
        var result_string = Regex.Replace(byte_str, @"^.*?<\?xml", @"<?xml");
        return result_string;
    }

    public XmlDocument xml_doc_from_xml_string(string xmlString)
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xmlString);
        return doc;
    }

    public Dictionary<string, LogDataSet> get_log_dataset_dict_from_xml(XmlDocument xmlDoc)
    {
        Dictionary<string, LogDataSet> dict = new Dictionary<string, LogDataSet>();
        XmlElement root = xmlDoc.DocumentElement;
        XmlNodeList nodeList = root.SelectNodes("//Logdata/LogDataSet");
        foreach (XmlNode xNode in nodeList)
        {
            //Console.WriteLine(xNode.InnerXml);
            LogDataSet tempLogDataSet = new LogDataSet(xNode);
            dict.Add(tempLogDataSet.device + "/" + tempLogDataSet.name, tempLogDataSet);
        }
        return dict;
    }

    public void update_data_dict()
    {
        string xmlString = get_xml_string_from_acessor(accessor);
        XmlDocument xmlDoc = xml_doc_from_xml_string(xmlString);
        data_dict = get_log_dataset_dict_from_xml(xmlDoc);
    }

    public Dictionary<string, Dictionary<string, string>> update_and_return_data_dict()
    {
        update_data_dict();
        return get_data_dicts();
    }

    public Dictionary<string, string> get_single_dict_from_key(string str_key)
    {
        LogDataSet log_obj = data_dict[str_key];

        return log_obj.ToDict();
    }
    public Dictionary<string, Dictionary<string, string>> get_data_dicts()
    {
        Dictionary<string, Dictionary<string, string>> dictionary = new Dictionary<string, Dictionary<string, string>>();
        foreach (var kvp in data_dict)
        {
            dictionary.Add(kvp.Key, kvp.Value.ToDict());
        }
        return dictionary;
    }

    public List<string> get_data_dict_keys()
    {
        List<string> list = new List<string>(data_dict.Keys);

        return list;
    }

}

