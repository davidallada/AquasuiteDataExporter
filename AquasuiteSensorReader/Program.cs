﻿using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

class Program
{
    static void Main(string[] args)
    {
        var fileName = "testexport";
        AquasuiteSharedMemoryExportHelper memory_helper = new AquasuiteSharedMemoryExportHelper(fileName);
        // memory_helper.print_all_data();
        //System.Threading.Thread.Sleep(5000);
        // memory_helper.update_data_dict();
        //memory_helper.print_all_data();
        for (int i = 0; i < 10; i++)
        {
            if (memory_helper.new_data_dict.ContainsKey("QUADRO/Water Temp"))
            {
                Console.WriteLine(memory_helper.new_data_dict["QUADRO/Water Temp"]["time"]);
            }
            System.Threading.Thread.Sleep(5000);
        }

        memory_helper.cancel_worker();


    }
}