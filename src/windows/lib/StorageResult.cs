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
using Newtonsoft.Json;

namespace JSONStoreWin8
{

    public sealed class StorageResult
    {
        public string value { get; private set; }
        public Status statusCode { get; private set; }

        private static readonly string[] StatusMessages = new string[] 
		{
			"No result",
			"OK",
			"Error"
		};

        public bool isSuccess
        {
            get { return this.statusCode == Status.NO_RESULT || this.statusCode == Status.OK; }
        }

        public StorageResult(Status status)
            : this(status, StorageResult.StatusMessages[(int) status])
        {
        }

        public StorageResult(Status status, object message)
        {
            this.value = JsonConvert.SerializeObject(message);
            this.statusCode = status;
        }
    }

    public enum Status : int
    {
        NO_RESULT = 0,
        OK,
        ERROR
    }

}