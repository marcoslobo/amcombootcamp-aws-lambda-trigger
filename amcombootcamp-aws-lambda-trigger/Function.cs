using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace amcom.bootcamp.aws.lambda.trigger
{
    public class Function
    {
        #region Variaveis 
        public string EmailFrom { get; set; }
        public string EmailSmtp { get; set; }
        public string EmailUser { get; set; }
        public string EmailPassword { get; set; }
        public string DynamoDbKeyId { get; set; }
        public string DynamoDbAccessKey { get; set; }
        public string DynamoDbTableName { get; set; }
        #endregion
        public Function()
        {

        }
        public async Task<string> FunctionHandler(DynamoDBEvent evnt, ILambdaContext context)
        {

            EmailFrom = Environment.GetEnvironmentVariable("email_from");
            EmailSmtp = Environment.GetEnvironmentVariable("email_smtp");
            EmailUser = Environment.GetEnvironmentVariable("email_user");
            EmailPassword = Environment.GetEnvironmentVariable("email_password");
            DynamoDbKeyId = Environment.GetEnvironmentVariable("dynamoDb_KeyId");
            DynamoDbAccessKey = Environment.GetEnvironmentVariable("dynamoDb_AccessKey");
            DynamoDbTableName = Environment.GetEnvironmentVariable("dynamoDb_TableName");

            try
            {
                foreach (var record in evnt.Records)
                {
                    if (record.Dynamodb.NewImage != null)
                    {
                        if (record.Dynamodb.NewImage.ContainsKey("email"))
                        {
                            await ThreatRecord(record);
                        }
                    }
                }

                return default;
            }
            catch (Exception e)
            {
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }
        }
        private async Task ThreatRecord(DynamoDBEvent.DynamodbStreamRecord record)
        {

            var emailTo = record.Dynamodb.NewImage.FirstOrDefault(a => a.Key == "email").Value.S;
            var action = record.Dynamodb.NewImage.FirstOrDefault(a => a.Key == "action").Value.S;

            string title, body;

            if (action == "AU")
            {
                body = await GetEmailMessageForSimpleAudit(emailTo);
            }
            else
            {
                var key = record.Dynamodb.NewImage.FirstOrDefault(a => a.Key == "key").Value.S;
                var data = DateTime.Parse(record.Dynamodb.NewImage.FirstOrDefault(a => a.Key == "createdAt").Value.S);
                var actionDescription = GetActionDescription(action);

                var dataFormatada = data.ToString("MM/dd/yyyy - HH:mm");
                body = GetEmailMessageForSimpleAudit(GetActionDescription(action), key, dataFormatada);
            }

            title = GetEmailTitle(GetActionDescriptionForTile(action));
            await SendEmailForClient(EmailFrom, emailTo, title, body);

        }

        private async Task SendEmailForClient(string emailFrom, string emailTo, string title, string body)
        {
            var smtpClient = new SmtpClient();
            await smtpClient.ConnectAsync(EmailSmtp, 465, true);
            await smtpClient.AuthenticateAsync(EmailUser, EmailPassword);

            await smtpClient.SendAsync(CreateMessage(emailFrom, emailTo, title, body));
            await smtpClient.DisconnectAsync(true);
        }

        private async Task<string> GetEmailMessageForSimpleAudit(string emailTo)
        {

            var client = new AmazonDynamoDBClient(DynamoDbKeyId, DynamoDbAccessKey);

            Table auditCatalog = Table.LoadTable(client, DynamoDbTableName);

            ScanFilter scanFilter = new();
            scanFilter.AddCondition("email", ScanOperator.Equal, emailTo);

            Search search = auditCatalog.Scan(scanFilter);

            var msgForAudit = new StringBuilder();

            List<Document> documentList = new();
            do
            {
                documentList = await search.GetNextSetAsync();

                foreach (var item in documentList)
                {
                    var action = item.FirstOrDefault(a => a.Key == "action").Value;
                    var key = "";

                    if (action != "AU")
                        key = item.FirstOrDefault(a => a.Key == "key").Value;

                    var data = DateTime.Parse(item.FirstOrDefault(a => a.Key == "createdAt").Value);
                    var dataFormatada = data.ToString("MM/dd/yyyy - HH:mm");

                    var msg = GetEmailMessageForSimpleAudit(GetActionDescription(action), key, dataFormatada) + "<BR />";

                    msgForAudit.AppendLine(msg);
                }
            } while (!search.IsDone);


            return msgForAudit.ToString();

        }

        private static string GetEmailMessageForSimpleAudit(string action, string key, string data)
        {
            if (action == "AU")
                return $"You {action} at {data}";
            else return $"You {action} {key} at {data}";
        }

        private static string GetEmailTitle(string description)
        {
            return $"Lobo Files - {description}";
        }

        private static string GetActionDescription(string action)
        {
            return action switch
            {
                "DL" => "removed the file",
                "U" => "added the file",
                "DW" => "downloaded the file",
                "AU" => "requested audit",
                _ => string.Empty,
            };
        }
        private static string GetActionDescriptionForTile(string action)
        {
            return action switch
            {
                "DL" => "File Removed",
                "U" => "File Added",
                "DW" => "File Downloaded",
                "AU" => "Audit Log",
                _ => string.Empty,
            };
        }
        private static MimeMessage CreateMessage(string sender, string receiver, string subject, string message)
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress("", sender));
            mimeMessage.To.Add(new MailboxAddress("", receiver));
            mimeMessage.Subject = subject;
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = message
            };
            mimeMessage.Body = bodyBuilder.ToMessageBody();
            return mimeMessage;
        }
    }

}
