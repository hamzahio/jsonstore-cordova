﻿/*
 *     Copyright 2016 IBM Corp.
 *     Licensed under the Apache License, Version 2.0 (the "License");
 *     you may not use this file except in compliance with the License.
 *     You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     Unless required by applicable law or agreed to in writing, software
 *     distributed under the License is distributed on an "AS IS" BASIS,
 *     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *     See the License for the specific language governing permissions and
 *     limitations under the License.
 */

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace JSONStoreWin8Lib.JSONStore
{
    class JSONStoreIndexer
    {

        public IDictionary<string, HashSet<string>> findIndexesFromSchema(JSONStoreSchema schema, JObject data)
        {
            IDictionary<string, HashSet<string>>  indexValuesDict = new Dictionary<string, HashSet<string>>();
    
            foreach(string idx in schema.getKeys()) {
                indexValuesDict.Add(idx, new HashSet<string>());
            }

            foreach(JProperty prop in data.Children()) {
                if (prop.Value.Type == JTokenType.Object)
                {
                    handleObject(indexValuesDict, (JObject)prop.Value, prop.Name);
                }
                else if (prop.Value.Type == JTokenType.Array)
                {
                    handleArray(indexValuesDict, (JArray)prop.Value, prop.Name);
                }
                else
                {
                    handleSimple(indexValuesDict, prop, null);
                }
            }

            return indexValuesDict;
        }

        private void handleObject(IDictionary<string, HashSet<string>>  indexValuesDict, JObject data, string parentPath)
        {
            foreach (JProperty prop in data.Children())
            {
                if (prop.Value.Type == JTokenType.Object)
                {
                    handleObject(indexValuesDict, (JObject)prop.Value, parentPath + "." + prop.Name.ToLower());
                }
                else if (prop.Value.Type == JTokenType.Array)
                {
                    handleArray(indexValuesDict, (JArray)prop.Value, parentPath + "." + prop.Name.ToLower());
                }
                else
                {
                    handleSimple(indexValuesDict, prop, parentPath);
                }
            }
        }

        private void handleArray(IDictionary<string, HashSet<string>>  indexValuesDict, JArray array, string parentPath)
        {
            foreach (JToken prop in array)
            {
                if (prop.Type == JTokenType.Object)
                {
                    handleObject(indexValuesDict, (JObject)prop, parentPath);
                }
                else if (prop.Type == JTokenType.Array)
                {
                    handleArray(indexValuesDict, (JArray)prop, parentPath);
                }
                else
                {
                    //In an array, if we just have a simple type, it can't be indexed.
                    //Example: {hobbies: [ 3, {k :v } ] }
                    //This case would match the '3', which we can't index.
                }
            }
        }

        private void handleSimple(IDictionary<string, HashSet<string>>  indexValuesDict, JProperty prop, string parentPath)
        {
            string path = null;
            if (parentPath == null)
            {
                path = prop.Name.ToLower();
            }
            else
            {
                path = parentPath.ToLower() + "." + prop.Name.ToLower();
            }

            if (indexValuesDict.ContainsKey(path))
            {
                string value = "";
                if (prop.Value.Type == JTokenType.Boolean)
                {
                    if (prop.Value.ToString().ToLower().Equals("true"))
                    {
                        value = "1";
                    }
                    else
                    {
                        value = "0";
                    }

                }
                else
                {
                    value = prop.Value.ToString();
                }
                indexValuesDict[path].Add(value);
            }
        }
    }
}
