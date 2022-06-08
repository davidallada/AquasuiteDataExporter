using System;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Text.RegularExpressions;
using System.Xml;

public class AquasuiteSharedMemoryExportHelper
{
    private readonly object data_dict_lock = new object();
    private string filename;
    public MemoryMappedFile mmapped_file;
    public MemoryMappedViewAccessor accessor;
    public Dictionary<string, LogDataSet> data_dict;
    private Dictionary<string, Dictionary<string, dynamic>> _new_data_dict = new Dictionary<string, Dictionary<string, dynamic>>();
    public Dictionary<string, Dictionary<string, dynamic>> new_data_dict
    {
        get
        {
            lock (data_dict_lock)
            {
                return _new_data_dict;
            }
        }
        set
        {
            lock (data_dict_lock)
            {
                _new_data_dict = value;
            }
        }
    }
    private System.ComponentModel.BackgroundWorker backgroundWorker1;


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
        this.new_data_dict = new Dictionary<string, Dictionary<string, dynamic>>();
        this.mmapped_file = mmapped_file_from_filename(in_filename);
        this.accessor = accessor_from_mmapped_file(mmapped_file);
        update_data_dict();

        InitializeBackgroundWorker();
        backgroundWorker1.RunWorkerAsync(in_filename);
    }

    //public Dictionary<string, Dictionary<string, dynamic>> 

    public void cancel_worker()
    {
        this.backgroundWorker1.CancelAsync();
    }
    public void InitializeBackgroundWorker()
    {
        this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
        backgroundWorker1.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);
        backgroundWorker1.WorkerReportsProgress = true;
        backgroundWorker1.WorkerSupportsCancellation = true;
    }

    private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
    {
        // Get the BackgroundWorker that raised this event.
        BackgroundWorker worker = sender as BackgroundWorker;
        
        // Get the filename of the file to read from
        string file_name = (string)e.Argument;
        MemoryMappedFile mem_mapped_file = MemoryMappedFile.OpenExisting(file_name);
        MemoryMappedViewAccessor mem_accessor = mem_mapped_file.CreateViewAccessor();

        while (true)
        {
            if (worker.CancellationPending == true)
            {
                e.Cancel = true;
                break;
            }
            else
            {
                //For some reason, the header starts with garbled bytes. Not sure why. Remove all of them
                var result_string = get_xml_string_from_acessor(mem_accessor);

                XmlDocument xmlDoc = xml_doc_from_xml_string(result_string);

                this.new_data_dict = get_dict_from_xml_doc(xmlDoc);
            }
            System.Threading.Thread.Sleep(1000);
        }
        // Temp, dont have to really return anything here
        e.Result = "";
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

    public Dictionary<string, Dictionary<string, dynamic>> get_dict_from_xml_doc(XmlDocument xmlDoc)
    {
        Dictionary<string, Dictionary<string, dynamic>> device_name_to_values_dict = new Dictionary<string, Dictionary<string, dynamic>>();
        XmlElement root = xmlDoc.DocumentElement;
        XmlNodeList nodeList = root.SelectNodes("//Logdata/LogDataSet");
        foreach (XmlNode xNode in nodeList)
        {
            Dictionary<string, dynamic> innerDict = new Dictionary<string, dynamic>();
            innerDict.Add("time", xNode.SelectSingleNode("./t").InnerText);
            innerDict.Add("value", xNode.SelectSingleNode("./value").InnerText);
            innerDict.Add("name", xNode.SelectSingleNode("./name").InnerText);
            innerDict.Add("unit", xNode.SelectSingleNode("./unit").InnerText);
            innerDict.Add("valueType", xNode.SelectSingleNode("./valueType").InnerText);
            innerDict.Add("device", xNode.SelectSingleNode("./device").InnerText);
            device_name_to_values_dict.Add(innerDict["device"] + "/" + innerDict["name"], innerDict);
        }
        return device_name_to_values_dict;
    }

    public void update_data_dict()
    {
        string xmlString = get_xml_string_from_acessor(accessor);
        XmlDocument xmlDoc = xml_doc_from_xml_string(xmlString);
        this.data_dict = get_log_dataset_dict_from_xml(xmlDoc);
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

    public void print_all_data()
    {
        foreach (var kvp in data_dict)
        {
            Console.WriteLine(kvp.Value.ToString());
        }
    }

    // BackgroundWorker with WorkerReportsProgress property and a DoWork and ProgressChanged function

}

