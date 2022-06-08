using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

public class AquasuiteSharedMemoryExportHelper
{
    private readonly object data_dict_lock = new object();
    private string filename;
    public MemoryMappedFile mmapped_file;
    public MemoryMappedViewAccessor accessor;
    // Time to wait to auto update background thread in milliseconds
    private int thread_wait_time = 1000;
    private Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> _data_dict = new Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>>();
    /**
     * data_dict has form of 
     * {
     *    <Device1> {
     *        <sensorname> {
     *              <sensordict>
     *        }
     *    }
     * }
    **/
    public Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> data_dict
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
        this.data_dict = new Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>>();
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

    public void updateThreadWaitTime(int milliseconds)
    {
        cancel_worker();
        init_class_vars(this.filename);
        this.thread_wait_time = milliseconds;
        start_worker();
    }
    public int getThreadWaitTime(int milliseconds)
    {
        return this.thread_wait_time;
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
            System.Threading.Thread.Sleep(this.thread_wait_time);
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

    public Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> get_dict_from_xml_doc(XmlDocument xmlDoc)
    {
        Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> whole_dict = new Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>>();
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

            if (whole_dict.ContainsKey(innerDict["device"]))
            {
                whole_dict[innerDict["device"]].Add(innerDict["name"], innerDict);
            }
            else
            {
                // If we dont have a dict for the given device, create one and add it to the whole_dict
                Dictionary<string, Dictionary<string, dynamic>> device_dict = new Dictionary<string, Dictionary<string, dynamic>>();
                device_dict.Add(innerDict["name"], innerDict);
                whole_dict.Add(innerDict["device"], device_dict);
            }
        }
        return whole_dict;
    }

    public void update_data_dict()
    {
        string xmlString = get_xml_string_from_acessor(accessor);
        XmlDocument xmlDoc = xml_doc_from_xml_string(xmlString);
        this.data_dict = get_dict_from_xml_doc(xmlDoc);
    }

    public Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> update_and_return_data_dict()
    {
        update_data_dict();
        return this.data_dict;
    }

    public List<string> get_device_keys()
    {
        List<string> list = new List<string>(data_dict.Keys);

        return list;
    }

    public List<string> get_sensor_keys_from_device_key(string device_key)
    {
        List<string> list = new List<string>();

        if (data_dict.ContainsKey(device_key))
        {
            return list.Concat(data_dict[device_key].Keys).ToList();
        }

        return list;
    }

    public string get_data_dict_as_string()
    {
        string ret_str = "";
        foreach (var device_dict in data_dict)
        {
            //Console.WriteLine(device_dict.Key);
            ret_str += String.Format("{0}\n", device_dict.Key);
            foreach (var sensor_dict in device_dict.Value)
            {
                //Console.WriteLine(String.Format("\t{0}", sensor_dict.Key));
                ret_str += String.Format("\t{0}\n", sensor_dict.Key);
                foreach (var sensor_kvp in sensor_dict.Value)
                {
                    //Console.WriteLine(String.Format("\t\t{0}: {1}", sensor_kvp.Key, sensor_kvp.Value));
                    String.Format("\t\t{0}: {1}\n", sensor_kvp.Key, sensor_kvp.Value);
                }

            }
        }

        return ret_str;
    }
    public void print_data_dict()
    {
        Console.WriteLine(get_data_dict_as_string());
    }

    public dynamic get_single_data_point(string device_name, string sensor_name, string field_name)
    {
        string return_str = "N/A";

        if (this.data_dict.ContainsKey(device_name))
        {
            var device_dict = this.data_dict[device_name];
            if (device_dict.ContainsKey(sensor_name))
            {
                var sensor_dict =  device_dict[sensor_name];
                if (sensor_dict.ContainsKey(field_name))
                {
                    return sensor_dict[field_name].ToString();
                }
            }
        }

        return return_str;
    }
}

