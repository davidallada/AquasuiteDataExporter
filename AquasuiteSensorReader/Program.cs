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
        for (int i = 0; i < 10; i++)
        {
            if (memory_helper.data_dict.ContainsKey("QUADRO"))
            {
                Console.WriteLine(memory_helper.data_dict["QUADRO"]["Water Temp"]["time"]);
                memory_helper.print_data_dict();
            }
            System.Threading.Thread.Sleep(5000);
        }

        memory_helper.cancel_worker();


    }
}