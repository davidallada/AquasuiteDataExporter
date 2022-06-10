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
        //var dictt = memory_helper.get_devices_sensor_fields_dict();
        //Console.WriteLine(dictt["QUADRO"]["Water Temp"][0]);
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine(memory_helper.get_single_data_point("QUADRO", "Water Temp", "time"));
            System.Threading.Thread.Sleep(1000);
        }
        Console.WriteLine("Update at half of th speed now");
        memory_helper.init_or_update_settings(fileName, 2000);
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine(memory_helper.get_single_data_point("QUADRO", "Water Temp", "time"));
            System.Threading.Thread.Sleep(1000);
        }

        memory_helper.init_or_update_settings(fileName, 1000);
        for (int i = 0; i < 10; i++)
        {
            memory_helper.update_and_return_data_dict();
            System.Threading.Thread.Sleep(1000);
        }

        //memory_helper.cancel_worker();


    }
}