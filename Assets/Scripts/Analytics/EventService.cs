using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Analytics
{
    public class EventService
    {
        private const string URL = "https://vk.com";
        private const string LOGS_BASE_DIRECTORY = "event_logs";
        private const int DELAY_TO_SEND_MESSAGES_PER_SECONDS = 5;

        private string _logStoragePath;
        
        private readonly HttpClient _client = new HttpClient();
        
        private bool _sendMessagesInProgress;
        private Task _saveEventsToFileTask;
        private Task _sendMessagesTask;
        private CancellationTokenSource _sendingMessagesCancellationTokenSource;
        private CancellationTokenSource _sendSavedMessagesCancellationTokenSource;

        public async Task Initialize()
        {
            _logStoragePath = Path.Combine(Application.persistentDataPath, LOGS_BASE_DIRECTORY, "influx_logs_storage");
            
            CreateLogsDirectoriesIfNotExist();
            
            _sendSavedMessagesCancellationTokenSource = new CancellationTokenSource();
            await TrySendMessagesFromSendingFolder(_sendSavedMessagesCancellationTokenSource.Token);
            
            _sendingMessagesCancellationTokenSource = new CancellationTokenSource();
            StartSendingMessages(_sendingMessagesCancellationTokenSource.Token);
        }

        public void Deinitialize()
        {
            _sendingMessagesCancellationTokenSource.Cancel();
            _sendSavedMessagesCancellationTokenSource.Cancel();
        }

        public void TrackEvent(string type, string data)
        {
            TrackEvent(new EventData(type, data));
        }
        
        public async void TrackEvent(EventData eventData)
        {
            if (_saveEventsToFileTask != null)
            {
                while (!_saveEventsToFileTask.IsCompleted)
                {
                    await Task.Yield();
                }
            }

            _saveEventsToFileTask = Task.Run(() => SaveEventToSendingFile(eventData));
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private async void StartSendingMessages(CancellationToken token)
        {
            while (true)
            {
                if(token.IsCancellationRequested) return;
                
                await Task.Delay(DELAY_TO_SEND_MESSAGES_PER_SECONDS * 1000, token);

                if (_sendMessagesTask is { IsCompleted: false })
                {
                    continue;
                }
                
                _sendMessagesTask = TrySendMessagesFromSendingFolder(token);
            }
        }

        private void CreateLogsDirectoriesIfNotExist()
        {
            Directory.CreateDirectory(_logStoragePath);
        }

        private async void SaveEventToSendingFile(EventData eventData)
        {
            while (_sendMessagesInProgress)
            {
                await Task.Yield();
            }
            
            var jsonString = JsonConvert.SerializeObject(eventData);
            var path = Path.Combine(_logStoragePath, Guid.NewGuid() + ".json");
            File.WriteAllText(path, jsonString);
        }
        
        private async Task TrySendMessagesFromSendingFolder(CancellationToken token)
        {
            _sendMessagesInProgress = true;
            
            var eventsToSend = new List<EventData>();
            
            var directoryInfo = new DirectoryInfo(_logStoragePath);
            var files = directoryInfo.GetFiles();

            if (files.Length <= 0)
            {
                _sendMessagesInProgress = false;

                return;
            }
            
            foreach (var fileInfo in files)
            {
                var message = File.ReadAllText(fileInfo.FullName);
                
                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }
                
                var eventData = JsonConvert.DeserializeObject<EventData>(message);
                eventsToSend.Add(eventData);
            }

            var isSuccess = await TrySendMessages(URL, eventsToSend, token);

            if (isSuccess)
            {
                ClearSendingFolder();
            }

            _sendMessagesInProgress = false;
        }
        
        private async Task<bool> TrySendMessages(string url, List<EventData> events, CancellationToken token)
        {
            if (token.IsCancellationRequested) return false;
            
            var content = new KeyValuePair<string, List<EventData>>("events", events);
            var json = JsonConvert.SerializeObject(content);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, httpContent, token);
            return response.IsSuccessStatusCode;
        }
        
        private void ClearSendingFolder()
        {
            var directoryInfo = new DirectoryInfo(_logStoragePath);
            var files = directoryInfo.GetFiles();
            foreach (var file in files)
            {
                file.Delete();
            }
        }
    }
}