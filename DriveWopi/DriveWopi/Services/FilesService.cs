using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DriveWopi.Models;
using DriveWopi.Exceptions;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DriveWopi.Services
{
    public class FilesService
    {
        public static string GenerateAuthorizationToken(User user)
        {
            return user.Authorization;
        }

        public static void HandleNotFoundCase(string fileId) 
        {
            try{
                Session s = Session.GetSessionFromRedis(fileId);
                s.DeleteSessionFromRedis();
                s.DeleteSessionFromAllSessionsInRedis(fileId);
                s.RemoveLocalFile();
            }
            catch(Exception ex){
                throw ex;
            }

        }
        public static bool UpdateFileInDrive(FileInfo fileInfo, string authorization, string fileId)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Set("cookie", authorization);
                    string url = Config.DriveUrl + "/api/files/" + fileId + "/reupload?size=" + fileInfo.Length;
                    byte[] responseArray = client.UploadFile(url, fileInfo.FullName);
                    Config.logger.LogDebug("UpdateFileInDrive success");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Config.logger.LogError("Got an exception in UploadFileInDrive:" + ex.Message);
                return false;
            }
        }
        public static void DownloadFileFromDrive(string idToDownload, string localPath, string authorization)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Set("cookie", authorization);
                    client.DownloadFile(Config.DriveUrl + "api/fs/file/" + idToDownload + "/download", localPath);
                }
                Config.logger.LogDebug("DownloadFileFromDrive success, fileId: "+idToDownload);
            }
            catch (Exception ex)
            {
                Config.logger.LogError("DownloadFileFromDrive fail, error: " + ex.Message);
                throw ex;
            }

        }

        public static void CopyTemplate(string template, string id)
        {
            try
            {
                string source = source = Config.TemplatesFolder + "/" + template;
                string dest = Config.Folder + "/" + id;
                File.Copy(source, dest, true);
            }
            catch (Exception ex)
            {
                Config.logger.LogError("CopyTemplate fail, error: " + ex.Message);
                throw ex;
            }
        }
        public static void CreateBlankFile(string path)
        {
            try
            {
                string type = path.Substring(path.LastIndexOf(".") + 1, path.Length - path.LastIndexOf(".") - 1).ToLower();
                string source;
                string dest = path;
                switch (type)
                {
                    case "docx":
                        source = Config.TemplatesFolder + "/blankDocx.docx";
                        break;
                    case "xlsx":
                        source = Config.TemplatesFolder + "/blankXlsx.xlsx";
                        break;
                    default:
                        source = Config.TemplatesFolder + "/blankDocx.docx";
                        break;
                }
                File.Copy(source, dest, true);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void Save(string id, byte[] newContent)
        {
            try
            {
                string filePath = GeneratePath(id);
                FileInfo fileInfo = new FileInfo(filePath);
                using (FileStream fileStream = fileInfo.Open(FileMode.Truncate))
                {
                    fileStream.Write(newContent, 0, newContent.Length);
                }
                Config.logger.LogDebug("Save file success, fileId: "+id);
            }
            catch (Exception ex)
            {
                Config.logger.LogError("Save file fail, error: " + ex.Message);
                throw ex;
            }

        }

        public static byte[] GetByteArrayFromStream(Stream input)
        {
            try
            {
                byte[] buffer = new byte[16 * 1024];
                using (MemoryStream ms = new MemoryStream())
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Config.logger.LogError("GetByteArrayFromStream fail, error: " + ex.Message);
                throw ex;
            }
        }

        public static byte[] GetFileContent(string filePath)
        {
            try
            {
                if (!FileExists(filePath))
                {
                    Config.logger.LogError("GetFileContent fail, file isnt exists");
                    throw new DriveFileNotFoundException(filePath);
                }
                //string filePath = GeneratePath(id);
                MemoryStream ms = new MemoryStream();
                FileInfo fileInfo = new FileInfo(filePath);
                lock (fileInfo)
                {
                    using (FileStream fileStream = fileInfo.OpenRead())
                    {
                        fileStream.CopyTo(ms);
                    }
                }
                ms.Position = 0;
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Config.logger.LogError("GetFileContent fail, error: " + ex.Message);
                throw ex;
            }

        }

        public static bool FileExists(string filePath)
        {
            try
            {
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Config.logger.LogError("FileExists fail, error: " + ex.Message);
                throw ex;
            }

        }

        public static void CreateEmptyFile(string path)
        {
            File.Create(path).Dispose();
        }


        public static string GeneratePath(string id)
        {
            return Config.Folder + "/" + id;
        }

        public static bool UpdateSessionInDrive(string id)
        {
            Session session = Session.GetSessionFromRedis(id);
            if(session == null){
                return true;
            }
            User userForUpload;
            if(session.Users != null && session.Users.Count > 0){
                userForUpload = session.Users[0];
            }
            else if(session.UserForUpload != null){
                userForUpload = session.UserForUpload;
            }
            else{
                return false;
            }
            bool updateResult = session.SaveToDrive(userForUpload);
            return updateResult;
    }

        public static void RemoveFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Config.logger.LogError("FileExists RemoveFile, error: " + ex.Message);
                throw ex;
            }

        }
    }
}
