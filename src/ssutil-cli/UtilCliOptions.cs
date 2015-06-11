﻿using System.Collections.Generic;
using Mono.Options;

namespace ssutil_cli
{
    public class UtilCliOptions
    {
        public static string URL { get { return "URL"; } }
        public static string LANG { get { return "lang"; } }
        public static string FILE { get { return "file"; } }

        public Dictionary<string, string> Options { get; set; }

        public UtilCliOptions()
        {
            Options = new Dictionary<string, string>();
            defaultOptionSet = new OptionSet()
            {
                {
                    "<>","Url of the ServiceStack endpoint",
                    val => { Options.Add(URL,val);}
                },
                {
                    "l|lang=","Specific language used when adding or updating a ServiceStack reference",
                    val => { Options.Add(LANG,val); }
                },
                {
                    "f|file=","ServiceStack reference file to update",
                    val => { Options.Add(FILE,val);}
                }
            };
        }

        public OptionSet DefaultOptionSet
        {
            get { return defaultOptionSet; }
        }

        private readonly OptionSet defaultOptionSet;

        public static UtilCliOptions GetInstance()
        {
            return _instance ?? (_instance = new UtilCliOptions());
        }

        private static UtilCliOptions _instance;
    }
}
