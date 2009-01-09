#region Apache License 2.0

// Copyright 2008-2009 Christian Rodemeyer
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using SharpSvn;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace SvnQuery
{
    public class SharpSvnApi : ISvnApi
    {
        readonly Uri uri;
        readonly string user;
        readonly string password;
        readonly List<SvnClient> clientPool = new List<SvnClient>();        

        public SharpSvnApi(string repositoryUrl) : this(repositoryUrl, "", "")
        {}

        public SharpSvnApi(string repositoryUri, string user, string password)
        {
            uri = new Uri(repositoryUri);
            this.user = user;
            this.password = password;
        }

        SvnClient AllocSvnClient()
        {
            SvnClient client = null;
            lock (clientPool)
            {
                int last = clientPool.Count - 1;
                if (last >= 0)
                {
                    client = clientPool[last];
                    clientPool.RemoveAt(last);
                }
            }

            if (client == null) client = new SvnClient();
            client.Authentication.UserNameHandlers += (s, e) => e.UserName = user;
            client.Authentication.UserNamePasswordHandlers += (s, e) => {e.UserName = user; e.Password = password;};
            return client;
        }

        void FreeSvnClient(SvnClient client)
        {
            lock (clientPool) clientPool.Add(client);
        }

        public int GetYoungestRevision()
        {
            SvnClient client = AllocSvnClient();
            SvnTarget target = new SvnUriTarget(uri);
            int youngest = 0;
            client.Info(target, (s, e) => youngest = (int) e.Revision);
            FreeSvnClient(client);
            return youngest;
        }

        public string GetLogMessage(int revision)
        {
            // Messages should be added from the onchange method (threadsafe)
            // if a message is not ready it will be fetched on demand
            throw new NotImplementedException();
        }

        public void ForEachChange(int firstRevision, int lastRevision, Action<PathChange> callback)
        {
            SvnClient client = AllocSvnClient();
            try
            {
                SvnLogArgs args = new SvnLogArgs(new SvnRevisionRange(firstRevision, lastRevision));
                args.StrictNodeHistory = false;
                args.RetrieveChangedPaths = true;
                client.Log(uri, args, delegate(object s, SvnLogEventArgs e)
                {                    
                    if (e == null || e.ChangedPaths == null) return;
                    foreach (var path in e.ChangedPaths)
                    {
                        PathChange change = new PathChange
                                                {
                                                    Revision = (int)e.Revision,
                                                    Path = path.Path,
                                                    IsCopy = path.CopyFromPath != null,
                                                };
                        switch (path.Action)
                        {
                            case SvnChangeAction.Add:     change.Change = Change.Add;     break;
                            case SvnChangeAction.Modify:  change.Change = Change.Modify;  break;
                            case SvnChangeAction.Delete:  change.Change = Change.Delete;  break;
                            case SvnChangeAction.Replace: change.Change = Change.Replace; break;
                            default:
                                throw new Exception("Invalid action on " + path.Path + "@" + e.Revision);
                        }
                        callback(change);
                    }
                });
            }
            finally
            {
                FreeSvnClient(client);
            }
        }

        public void ForEachChild(string path, int revision, Action<PathChange> callback)
        {
            SvnClient client = AllocSvnClient();
            SvnTarget target = new SvnUriTarget(new Uri(uri + path), revision);
            try
            {
                SvnListArgs args = new SvnListArgs {Depth = SvnDepth.Infinity, Revision = revision};
                client.List(target, args, delegate(object s, SvnListEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Path))
                      callback(new PathChange {Change = Change.Add, Path = e.BasePath + "/" + e.Path , IsCopy = false, Revision = revision});
                });
            }
            finally
            {
                FreeSvnClient(client);
            }
        }


        public PathData GetPathData(string path, int revision)
        {
            SvnClient client = AllocSvnClient();
            SvnTarget target = new SvnUriTarget(new Uri(uri + path), revision);
            PathData data = null;
            try
            {
                SvnInfoEventArgs info;
                client.GetInfo(target, out info);

                data = new PathData();
                data.Path = path;
                data.Size = (int)info.RepositorySize;
                data.Author = info.LastChangeAuthor;
                data.Timestamp = info.LastChangeTime;
                data.RevisionFirst = (int)info.LastChangeRevision;
                data.RevisionLast = revision;
                data.IsDirectory = info.NodeKind == SvnNodeKind.Directory;

                Collection<SvnPropertyListEventArgs> pc;
                client.GetPropertyList(target, out pc);
                foreach (var proplist in pc)
                {
                    foreach (var property in proplist.Properties)
                    {
                        data.Properties.Add(property.Key, property.StringValue);
                    }
                }

                string mime;
                data.Properties.TryGetValue("svn:mime-type", out mime);
                const int MaxFileSize = 128 * 1024 * 1024;
                if (!data.IsDirectory && (string.IsNullOrEmpty(mime) || mime.StartsWith("text/")) && data.Size < MaxFileSize)
                {
                    MemoryStream stream = new MemoryStream(data.Size);
                    client.Write(target, stream);
                    stream.Position = 0;
                    data.Text = new StreamReader(stream).ReadToEnd(); // default utf-8 encoding, does not work with codepages
                    stream.Dispose();
                }
            }
            catch (SvnException x)
            {
                if (x.SvnErrorCode != SvnErrorCode.SVN_ERR_RA_ILLEGAL_URL) throw;                
            }
            finally
            {
                FreeSvnClient(client);
            }
            return data;
        }

    }
}