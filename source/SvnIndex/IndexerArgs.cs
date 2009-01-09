﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SvnQuery
{
    
    public class IndexerArgs
    {
        public Indexer.Command Command;
        public string IndexPath;
        public string RepositoryUri;
        public string User;
        public string Password;
        public Regex Filter; // pathes that match this regex are not indexed
        public int MaxRevision = 99999999;
        public int MaxThreads = 16;
        public int Optimize = 25; // number of revisions that lead to optimization

        public IndexerArgs(string[] args)
        {
            bool allMandatoryArgumentsFound = false;
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i][0] == '-')
                {
                    if (args[i].Length != 2) throw new Exception("Invalid Option " + args[i]);
                    switch (char.ToLowerInvariant(args[i][1]))
                    {
                        case 'r':
                            MaxRevision = int.Parse(args[i + 1]);
                            break;
                        case 'f':
                            Filter = new Regex(args[i + 1], RegexOptions.Compiled);
                            break;
                        case 'u':
                            User = args[i + 1];
                            break;
                        case 'p':
                            Password = args[i + 1];
                            break;
                        case 't':
                            MaxThreads = int.Parse(args[i + 1]);
                            break;
                        case 'o':
                            Optimize = int.Parse(args[i + 1]);
                            break;
                    }
                }
                else switch(i)
                {
                    case 0:
                        Command = (Indexer.Command) Enum.Parse(typeof (Indexer.Command), args[i], true);
                        break;
                    case 1:
                        IndexPath = Path.GetFullPath(args[i]);
                        break;
                    case 2:
                        RepositoryUri = args[i];
                        allMandatoryArgumentsFound = true;
                        break;                        
                }
            }
            if (!allMandatoryArgumentsFound) throw new Exception("Missing arguments");
        }
    }
}
