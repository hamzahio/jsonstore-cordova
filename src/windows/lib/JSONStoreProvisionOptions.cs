/*
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

namespace JSONStoreWin8Lib.JSONStore
{
    public class JSONStoreProvisionOptions
    {
        // security
        public string username { get; set; }
        public string collectionPassword { get; set; }
        public string secureRandom { get; set; }
        public bool localKeyGen { get; set; }

        public IDictionary<string, string> additionalSearchFields { get; set; }
        public bool dropCollection { get; set; }
    }
}
