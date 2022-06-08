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
    private Dictionary<string, Dictionary<string, dynamic>> _data_dict = new Dictionary<string, Dictionary<string, dynamic>>();
    public Dictionary<string, Dictionary<string, dynamic>> data_dict
    {
        get
        {
            // Ensure we are accessing data dict in a thread safe manner
            lock (data_dict_lock)
            {
                return _data_dict;
            }
        }
        set
        {
            // Ensure we are setting data dict in a thread safe manner
            lock (data_dict_lock)
            {
                _data_dict = value;
            }
        }
    }
    private System.ComponentModel.BackgroundWorker backgroundWorker1;


    private void init_class_vars(string in_filename)
    {
        this.filename = in_filename;
        this.data_dict = new Dictionary<string, Dictionary<string, dynamic>>();
        this.mmapped_file = mmapped_file_from_filename(in_filename);
        this.accessor = accessor_from_mmapped_file(mmapped_file);
        update_data_dict();

        InitializeBackgroundWorker();
    }
    public AquasuiteSharedMemoryExportHelper(string in_filename)
    {
        init_class_vars(in_filename);
        start_worker();
    }

    public void start_worker()
    {
        backgroundWorker1.RunWorkerAsync(filename);
    }

    public void cancel_worker()
    {
        this.backgroundWorker1.CancelAsync();
    }

    public void update_filename(string in_filename)
    {
        cancel_worker();
        init_class_vars(in_filename);
        start_worker();
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
        // Have a background worker start setting the data dict automatically

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

                this.data_dict = get_dict_from_xml_doc(xmlDoc);
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
        this.data_dict = get_dict_from_xml_doc(xmlDoc);
    }

    public Dictionary<string, Dictionary<string, dynamic>> update_and_return_data_dict()
    {
        update_data_dict();
        return this.data_dict;
    }

    public Dictionary<string, dynamic> get_single_dict_from_key(string str_key)
    {
        return this.data_dict["str_key"];
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

