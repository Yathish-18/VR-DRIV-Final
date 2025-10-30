// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PampelGames.Shared.Tools
{
    public static class PGSaveSystem
    {
        /// <summary>
        ///     Saves Serializable data.
        /// </summary>
        /// <param name="data">Must be an instance of a serializable class and cannot be a collection (however, the class can contain collections).</param>
        /// <param name="saveFilePrefix">Prefix for the file name.</param>
        /// <param name="slotIndex">Slot index for the file, also included in the name.</param>
        /// <param name="encryptionKey">Encryption key for the file.</param>
        public static void Save<T>(T data, string saveFilePrefix, int slotIndex, string encryptionKey = "PGSaveSystemEncryptionKey") where T : new()
        {
            var json = JsonUtility.ToJson(data);

            if (string.IsNullOrEmpty(json) || json == "{}") throw new InvalidOperationException("Data is not serializable to JSON.");

            var encryptedData = Encryption.Encrypt(Encoding.UTF8.GetBytes(json), encryptionKey);

            var saveFileName = saveFilePrefix + "_" + slotIndex + ".dat";
            var filePath = Application.persistentDataPath + "/" + saveFileName;
            File.WriteAllBytes(filePath, encryptedData);
        }

        /// <summary>
        ///     Loads the saved data.
        /// </summary>
        /// <param name="saveFilePrefix">Prefix for the file name.</param>
        /// <param name="slotIndex">Slot index for the file, also included in the name.</param>
        /// <param name="encryptionKey">Encryption key for the file.</param>
        /// <returns>Loaded data.</returns>
        public static T Load<T>(string saveFilePrefix, int slotIndex, string encryptionKey = "PGSaveSystemEncryptionKey") where T : new()
        {
            var saveFileName = saveFilePrefix + "_" + slotIndex + ".dat";
            var saveFilePath = Application.persistentDataPath + "/" + saveFileName;

            if (File.Exists(saveFilePath))
            {
                var encryptedData = File.ReadAllBytes(saveFilePath);
                var decryptedData = Encryption.Decrypt(encryptedData, encryptionKey);
                var json = Encoding.UTF8.GetString(decryptedData);
                var data = JsonUtility.FromJson<T>(json);
                return data;
            }

            return new T();
        }
    }
}