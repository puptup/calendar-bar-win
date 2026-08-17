using System.Text;
using System.Text.RegularExpressions;

namespace CalendarBar;

public sealed class WbxmlException : Exception
{
    public WbxmlException(string message) : base(message) { }
}

public static class WbxmlCodec
{
    private const byte SwitchPage = 0x00;
    private const byte End = 0x01;
    private const byte StrI = 0x03;
    private const byte Opaque = 0xC3;
    private const byte TagContent = 0x40;

    private sealed class CodePage
    {
        public string Namespace { get; }
        public Dictionary<string, byte> Tokens { get; }
        public Dictionary<byte, string> Names { get; }

        public CodePage(string ns, Dictionary<string, byte> tokens)
        {
            Namespace = ns;
            Tokens = tokens;
            Names = tokens.ToDictionary(kv => kv.Value, kv => kv.Key);
        }
    }

    private sealed class XmlNode
    {
        public bool IsText;
        public string Name = "";
        public string Namespace = "";
        public string Text = "";
        public List<XmlNode> Children = [];
    }

    private static readonly CodePage[] CodePages =
    [
        new("AirSync", new()
        {
            ["Sync"] = 0x05, ["Responses"] = 0x06, ["Add"] = 0x07, ["Change"] = 0x08, ["Delete"] = 0x09,
            ["Fetch"] = 0x0a, ["SyncKey"] = 0x0b, ["ClientId"] = 0x0c, ["ServerId"] = 0x0d, ["Status"] = 0x0e,
            ["Collection"] = 0x0f, ["Class"] = 0x10, ["CollectionId"] = 0x12, ["GetChanges"] = 0x13,
            ["MoreAvailable"] = 0x14, ["WindowSize"] = 0x15, ["Commands"] = 0x16, ["Options"] = 0x17,
            ["FilterType"] = 0x18, ["Conflict"] = 0x1b, ["Collections"] = 0x1c, ["ApplicationData"] = 0x1d,
            ["DeletesAsMoves"] = 0x1e, ["Supported"] = 0x20, ["SoftDelete"] = 0x21, ["MIMESupport"] = 0x22,
            ["MIMETruncation"] = 0x23, ["Wait"] = 0x24, ["Limit"] = 0x25, ["Partial"] = 0x26,
            ["ConversationMode"] = 0x27, ["MaxItems"] = 0x28, ["HeartbeatInterval"] = 0x29
        }),
        new("Contacts", []),
        new("Email", new()
        {
            ["Attachment"] = 0x05, ["Attachments"] = 0x06, ["AttName"] = 0x07, ["AttSize"] = 0x08,
            ["Att0id"] = 0x09, ["AttMethod"] = 0x0a, ["Body"] = 0x0c, ["BodySize"] = 0x0d,
            ["BodyTruncated"] = 0x0e, ["DateReceived"] = 0x0f, ["DisplayName"] = 0x10, ["DisplayTo"] = 0x11,
            ["Importance"] = 0x12, ["MessageClass"] = 0x13, ["Subject"] = 0x14, ["Read"] = 0x15, ["To"] = 0x16,
            ["Cc"] = 0x17, ["From"] = 0x18, ["ReplyTo"] = 0x19, ["AllDayEvent"] = 0x1a, ["Categories"] = 0x1b,
            ["Category"] = 0x1c, ["DtStamp"] = 0x1d, ["EndTime"] = 0x1e, ["InstanceType"] = 0x1f,
            ["BusyStatus"] = 0x20, ["Location"] = 0x21, ["MeetingRequest"] = 0x22, ["Organizer"] = 0x23,
            ["RecurrenceId"] = 0x24, ["Reminder"] = 0x25, ["ResponseRequested"] = 0x26, ["Recurrences"] = 0x27,
            ["Recurrence"] = 0x28, ["Type"] = 0x29, ["Until"] = 0x2a, ["Occurrences"] = 0x2b, ["Interval"] = 0x2c,
            ["DayOfWeek"] = 0x2d, ["DayOfMonth"] = 0x2e, ["WeekOfMonth"] = 0x2f, ["MonthOfYear"] = 0x30,
            ["StartTime"] = 0x31, ["Sensitivity"] = 0x32, ["TimeZone"] = 0x33, ["GlobalObjId"] = 0x34,
            ["ThreadTopic"] = 0x35, ["MIMEData"] = 0x36, ["MIMETruncated"] = 0x37, ["MIMESize"] = 0x38,
            ["InternetCPID"] = 0x39, ["Flag"] = 0x3a, ["Status"] = 0x3b, ["ContentClass"] = 0x3c,
            ["FlagType"] = 0x3d, ["CompleteTime"] = 0x3e, ["DisallowNewTimeProposal"] = 0x3f
        }),
        new("", []),
        new("Calendar", new()
        {
            ["TimeZone"] = 0x05, ["AllDayEvent"] = 0x06, ["Attendees"] = 0x07, ["Attendee"] = 0x08,
            ["Email"] = 0x09, ["Name"] = 0x0a, ["Body"] = 0x0b, ["BodyTruncated"] = 0x0c, ["BusyStatus"] = 0x0d,
            ["Categories"] = 0x0e, ["Category"] = 0x0f, ["Rtf"] = 0x10, ["DtStamp"] = 0x11, ["EndTime"] = 0x12,
            ["Exception"] = 0x13, ["Exceptions"] = 0x14, ["Deleted"] = 0x15, ["ExceptionStartTime"] = 0x16,
            ["Location"] = 0x17, ["MeetingStatus"] = 0x18, ["OrganizerEmail"] = 0x19, ["OrganizerName"] = 0x1a,
            ["Recurrence"] = 0x1b, ["Type"] = 0x1c, ["Until"] = 0x1d, ["Occurrences"] = 0x1e, ["Interval"] = 0x1f,
            ["DayOfWeek"] = 0x20, ["DayOfMonth"] = 0x21, ["WeekOfMonth"] = 0x22, ["MonthOfYear"] = 0x23,
            ["Reminder"] = 0x24, ["Sensitivity"] = 0x25, ["Subject"] = 0x26, ["StartTime"] = 0x27, ["UID"] = 0x28,
            ["AttendeeStatus"] = 0x29, ["AttendeeType"] = 0x2a, ["DisallowNewTimeProposal"] = 0x33,
            ["ResponseRequested"] = 0x34, ["AppointmentReplyTime"] = 0x35, ["ResponseType"] = 0x36,
            ["CalendarType"] = 0x37, ["IsLeapMonth"] = 0x38, ["FirstDayOfWeek"] = 0x39,
            ["OnlineMeetingConfLink"] = 0x3a, ["OnlineMeetingExternalLink"] = 0x3b, ["ClientUid"] = 0x3c
        }),
        new("Move", []),
        new("GetItemEstimate", new()
        {
            ["GetItemEstimate"] = 0x05, ["Version"] = 0x06, ["Collections"] = 0x07, ["Collection"] = 0x08,
            ["Class"] = 0x09, ["CollectionId"] = 0x0a, ["DateTime"] = 0x0b, ["Estimate"] = 0x0c,
            ["Response"] = 0x0d, ["Status"] = 0x0e
        }),
        new("FolderHierarchy", new()
        {
            ["DisplayName"] = 0x07, ["ServerId"] = 0x08, ["ParentId"] = 0x09, ["Type"] = 0x0a, ["Status"] = 0x0c,
            ["Changes"] = 0x0e, ["Add"] = 0x0f, ["Delete"] = 0x10, ["Update"] = 0x11, ["SyncKey"] = 0x12,
            ["FolderCreate"] = 0x13, ["FolderDelete"] = 0x14, ["FolderUpdate"] = 0x15, ["FolderSync"] = 0x16,
            ["Count"] = 0x17
        }),
        new("MeetingResponse", new()
        {
            ["CalendarId"] = 0x05, ["CollectionId"] = 0x06, ["MeetingResponse"] = 0x07,
            ["RequestId"] = 0x08, ["Request"] = 0x09, ["Result"] = 0x0a, ["Status"] = 0x0b,
            ["UserResponse"] = 0x0c, ["InstanceId"] = 0x0e
        }),
        new("Tasks", []),
        new("ResolveRecipients", []),
        new("ValidateCert", []),
        new("Contacts2", []),
        new("Ping", []),
        new("Provision", new()
        {
            ["Provision"] = 0x05, ["Policies"] = 0x06, ["Policy"] = 0x07, ["PolicyType"] = 0x08,
            ["PolicyKey"] = 0x09, ["Data"] = 0x0a, ["Status"] = 0x0b, ["RemoteWipe"] = 0x0c,
            ["EASProvisionDoc"] = 0x0d, ["DevicePasswordEnabled"] = 0x0e,
            ["AlphanumericDevicePasswordRequired"] = 0x0f, ["RequireStorageCardEncryption"] = 0x10,
            ["PasswordRecoveryEnabled"] = 0x11, ["AttachmentsEnabled"] = 0x13,
            ["MinDevicePasswordLength"] = 0x14, ["MaxInactivityTimeDeviceLock"] = 0x15,
            ["MaxDevicePasswordFailedAttempts"] = 0x16, ["MaxAttachmentSize"] = 0x17,
            ["AllowSimpleDevicePassword"] = 0x18, ["DevicePasswordExpiration"] = 0x19,
            ["DevicePasswordHistory"] = 0x1a, ["AllowStorageCard"] = 0x1b, ["AllowCamera"] = 0x1c,
            ["RequireDeviceEncryption"] = 0x1d, ["AllowUnsignedApplications"] = 0x1e,
            ["AllowUnsignedInstallationPackages"] = 0x1f, ["MinDevicePasswordComplexCharacters"] = 0x20,
            ["AllowWiFi"] = 0x21, ["AllowTextMessaging"] = 0x22, ["AllowPOPIMAPEmail"] = 0x23,
            ["AllowBluetooth"] = 0x24, ["AllowIrDA"] = 0x25, ["RequireManualSyncWhenRoaming"] = 0x26,
            ["AllowDesktopSync"] = 0x27, ["MaxCalendarAgeFilter"] = 0x28, ["AllowHTMLEmail"] = 0x29,
            ["MaxEmailAgeFilter"] = 0x2a, ["MaxEmailBodyTruncationSize"] = 0x2b,
            ["MaxEmailHTMLBodyTruncationSize"] = 0x2c, ["RequireSignedSMIMEMessages"] = 0x2d,
            ["RequireEncryptedSMIMEMessages"] = 0x2e, ["RequireSignedSMIMEAlgorithm"] = 0x2f,
            ["RequireEncryptionSMIMEAlgorithm"] = 0x30,
            ["AllowSMIMEEncryptionAlgorithmNegotiation"] = 0x31, ["AllowSMIMESoftCerts"] = 0x32,
            ["AllowBrowser"] = 0x33, ["AllowConsumerEmail"] = 0x34, ["AllowRemoteDesktop"] = 0x35,
            ["AllowInternetSharing"] = 0x36, ["UnapprovedInROMApplicationList"] = 0x37,
            ["ApplicationName"] = 0x38, ["ApprovedApplicationList"] = 0x39, ["Hash"] = 0x3a
        }),
        new("Search", []),
        new("Gal", []),
        new("AirSyncBase", new()
        {
            ["BodyPreference"] = 0x05, ["Type"] = 0x06, ["TruncationSize"] = 0x07, ["AllOrNone"] = 0x08,
            ["Body"] = 0x0a, ["Data"] = 0x0b, ["EstimatedDataSize"] = 0x0c, ["Truncated"] = 0x0d,
            ["Attachments"] = 0x0e, ["Attachment"] = 0x0f, ["DisplayName"] = 0x10, ["FileReference"] = 0x11,
            ["Method"] = 0x12, ["ContentId"] = 0x13, ["ContentLocation"] = 0x14, ["IsInline"] = 0x15,
            ["NativeBodyType"] = 0x16, ["ContentType"] = 0x17, ["Preview"] = 0x18,
            ["BodyPartPreference"] = 0x19, ["BodyPart"] = 0x1a, ["Status"] = 0x1b
        }),
        new("Settings", new()
        {
            ["Settings"] = 0x05, ["Status"] = 0x06, ["Get"] = 0x07, ["Set"] = 0x08, ["DeviceInformation"] = 0x16,
            ["Model"] = 0x17, ["IMEI"] = 0x18, ["FriendlyName"] = 0x19, ["OS"] = 0x1a, ["OSLanguage"] = 0x1b,
            ["PhoneNumber"] = 0x1c, ["UserAgent"] = 0x20, ["MobileOperator"] = 0x22
        }),
        new("DocumentLibrary", []),
        new("ItemOperations", new()
        {
            ["ItemOperations"] = 0x05, ["Fetch"] = 0x06, ["Store"] = 0x07, ["Options"] = 0x08,
            ["Range"] = 0x09, ["Total"] = 0x0a, ["Properties"] = 0x0b, ["Data"] = 0x0c,
            ["Status"] = 0x0d, ["Response"] = 0x0e, ["Version"] = 0x0f, ["Schema"] = 0x10,
            ["Part"] = 0x11, ["EmptyFolderContents"] = 0x12, ["DeleteSubFolders"] = 0x13,
            ["UserName"] = 0x14, ["Password"] = 0x15, ["Move"] = 0x16, ["DstFldId"] = 0x17,
            ["ConversationId"] = 0x18, ["MoveAlways"] = 0x19
        }),
        new("ComposeMail", new()
        {
            ["SendMail"] = 0x05, ["SmartForward"] = 0x06, ["SmartReply"] = 0x07,
            ["SaveInSentItems"] = 0x08, ["ReplaceMime"] = 0x09, ["Type"] = 0x0a,
            ["Source"] = 0x0b, ["FolderId"] = 0x0c, ["ItemId"] = 0x0d, ["LongId"] = 0x0e,
            ["InstanceId"] = 0x0f, ["MIME"] = 0x10, ["ClientId"] = 0x11, ["Status"] = 0x12,
            ["AccountId"] = 0x13
        }),
        new("Email2", new()
        {
            ["UmCallerID"] = 0x05, ["UmUserNotes"] = 0x06, ["UmAttDuration"] = 0x07, ["UmAttOrder"] = 0x08,
            ["ConversationId"] = 0x09, ["ConversationIndex"] = 0x0a, ["LastVerbExecuted"] = 0x0b,
            ["LastVerbExecutionTime"] = 0x0c, ["ReceivedAsBcc"] = 0x0d, ["Sender"] = 0x0e,
            ["CalendarType"] = 0x0f, ["IsLeapMonth"] = 0x10, ["AccountId"] = 0x11, ["FirstDayOfWeek"] = 0x12,
            ["MeetingMessageType"] = 0x13
        }),
        new("Notes", []),
        new("RightsManagement", [])
    ];

    private static readonly Dictionary<string, int> NamespaceToPage = CodePages
        .Select((p, i) => (p.Namespace, i))
        .Where(x => !string.IsNullOrEmpty(x.Namespace))
        .ToDictionary(x => x.Namespace, x => x.i);

    private static string NormalizeNamespace(string value)
    {
        var ns = value.Trim();
        return ns.EndsWith(':') ? ns[..^1] : ns;
    }

    private static int? PageIndex(string ns) =>
        NamespaceToPage.TryGetValue(NormalizeNamespace(ns), out var i) ? i : null;

    public static byte[] Encode(string xml)
    {
        var nodes = ParseXml(xml);
        var bytes = new List<byte> { 0x03, 0x01, 0x6a, 0x00 };
        var page = 0;
        foreach (var node in nodes)
            AppendNode(bytes, node, "AirSync", ref page);
        return bytes.ToArray();
    }

    public static string Decode(byte[] data)
    {
        var offset = 4;
        var page = 0;
        var root = ParseDocument(data, ref offset, ref page);
        if (root is null) return "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + RenderNode(root, includeXmlns: true);
    }

    private static void AppendNode(List<byte> bytes, XmlNode node, string inheritedNamespace, ref int currentPage)
    {
        if (node.IsText)
        {
            var text = node.Text.Trim();
            if (text.Length == 0) return;
            bytes.Add(StrI);
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
            bytes.Add(0x00);
            return;
        }

        var resolved = NormalizeNamespace(string.IsNullOrEmpty(node.Namespace) ? inheritedNamespace : node.Namespace);
        var pageIndex = PageIndex(resolved) ?? throw new WbxmlException($"Unsupported WBXML namespace: {resolved}");
        if (currentPage != pageIndex)
        {
            bytes.Add(SwitchPage);
            bytes.Add((byte)pageIndex);
            currentPage = pageIndex;
        }

        var page = CodePages[pageIndex];
        if (!page.Tokens.TryGetValue(node.Name, out var token))
            throw new WbxmlException($"Unsupported WBXML tag: {resolved}:{node.Name}");

        var hasContent = node.Children.Count > 0;
        bytes.Add(hasContent ? (byte)(token | TagContent) : token);
        if (!hasContent) return;
        foreach (var child in node.Children)
            AppendNode(bytes, child, resolved, ref currentPage);
        bytes.Add(End);
    }

    private static XmlNode? ParseDocument(byte[] bytes, ref int offset, ref int currentPage)
    {
        var stack = new List<XmlNode>();
        XmlNode? root = null;

        while (offset < bytes.Length)
        {
            var token = bytes[offset++];
            if (token == SwitchPage)
            {
                currentPage = bytes[offset++];
                continue;
            }
            if (token == End)
            {
                if (stack.Count == 0) continue;
                var completed = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                if (stack.Count > 0) stack[^1].Children.Add(completed);
                else root = completed;
                continue;
            }
            if (token == StrI)
            {
                var value = ReadInlineString(bytes, ref offset);
                if (stack.Count > 0)
                    stack[^1].Children.Add(new XmlNode { IsText = true, Text = value });
                continue;
            }
            if (token == Opaque)
            {
                var length = ReadMultiByteInt(bytes, ref offset);
                var value = Encoding.UTF8.GetString(bytes, offset, length);
                offset += length;
                if (stack.Count > 0)
                    stack[^1].Children.Add(new XmlNode { IsText = true, Text = value });
                continue;
            }

            var hasContent = (token & TagContent) != 0;
            var tagToken = (byte)(token & 0x3f);
            var page = CodePages[currentPage];
            var name = page.Names.TryGetValue(tagToken, out var n) ? n : $"Unknown0x{tagToken:X2}";
            var element = new XmlNode { Name = name, Namespace = page.Namespace };
            if (hasContent) stack.Add(element);
            else if (stack.Count > 0) stack[^1].Children.Add(element);
            else root = element;
        }

        return root;
    }

    private static string RenderNode(XmlNode node, bool includeXmlns)
    {
        if (node.IsText) return EscapeXml(node.Text);
        var xmlns = includeXmlns ? $" xmlns=\"{node.Namespace}:\"" : "";
        if (node.Children.Count == 0) return $"<{node.Name}{xmlns}/>";
        var childText = string.Concat(node.Children.Select(c => RenderNode(c, false)));
        return $"<{node.Name}{xmlns}>{childText}</{node.Name}>";
    }

    /// ponytail: one self-check for the codec; expand to xUnit if the protocol tables grow.
    public static void SelfCheck()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\"><Collections><Collection><SyncKey>0</SyncKey><CollectionId>col-1</CollectionId></Collection></Collections></Sync>";
        var decoded = Decode(Encode(xml));
        if (!decoded.Contains("<SyncKey>0</SyncKey>") || !decoded.Contains("<CollectionId>col-1</CollectionId>"))
            throw new WbxmlException("WBXML round-trip self-check failed");

        var readXml = ActiveSyncParser.BuildReadChangeRequestXml("5", "col-1", ["srv-9"], true);
        var readDecoded = Decode(Encode(readXml));
        if (!readDecoded.Contains("<Read>1</Read>"))
            throw new WbxmlException("WBXML read-change self-check failed");

        var bytes = new byte[]
        {
            0x03, 0x01, 0x6a, 0x00,
            0x45,
            0x7f, 0x03, 0x78, 0x00, 0x01,
            0x4b, 0x03, 0x37, 0x00, 0x01,
            0x01
        };
        var unknown = Decode(bytes);
        if (!unknown.Contains("Unknown0x3F") || !unknown.Contains("<SyncKey>7</SyncKey>"))
            throw new WbxmlException("WBXML unknown-token self-check failed");
    }

    private static List<XmlNode> ParseXml(string xml)
    {
        var document = Regex.Replace(xml, @"<\?xml[\s\S]*?\?>", "");
        document = document.Replace("\r\n", "").Replace("\n", "").Trim();

        var stack = new List<XmlNode>();
        var roots = new List<XmlNode>();
        var index = 0;

        while (index < document.Length)
        {
            if (document[index] == '<')
            {
                var end = document.IndexOf('>', index);
                if (end < 0) throw new WbxmlException("Malformed XML while encoding WBXML");
                var rawTag = document[(index + 1)..end].Trim();

                if (rawTag.StartsWith('/'))
                {
                    if (stack.Count == 0) throw new WbxmlException("Malformed XML while encoding WBXML");
                    var closed = stack[^1];
                    stack.RemoveAt(stack.Count - 1);
                    if (stack.Count > 0) stack[^1].Children.Add(closed);
                    else roots.Add(closed);
                }
                else
                {
                    var selfClosing = rawTag.EndsWith('/');
                    var tagContent = selfClosing ? rawTag[..^1].Trim() : rawTag;
                    var parts = SplitTag(tagContent);
                    var rawName = parts.FirstOrDefault() ?? "";
                    var attributes = string.Join(' ', parts.Skip(1));

                    var ns = "AirSync";
                    var extracted = ExtractXmlnsNamespace(attributes);
                    if (extracted is not null) ns = extracted;
                    else if (rawName.Contains(':'))
                        ns = PrefixToNamespace(rawName.Split(':')[0]);
                    else if (stack.Count > 0 && !string.IsNullOrEmpty(stack[^1].Namespace))
                        ns = stack[^1].Namespace;

                    var localName = rawName.Contains(':') ? rawName.Split(':').Last() : rawName;
                    var element = new XmlNode { Name = localName, Namespace = ns };
                    if (selfClosing)
                    {
                        if (stack.Count > 0) stack[^1].Children.Add(element);
                        else roots.Add(element);
                    }
                    else stack.Add(element);
                }

                index = end + 1;
                continue;
            }

            var nextTag = document.IndexOf('<', index);
            if (nextTag < 0) nextTag = document.Length;
            var text = document[index..nextTag];
            if (!string.IsNullOrWhiteSpace(text) && stack.Count > 0)
                stack[^1].Children.Add(new XmlNode { IsText = true, Text = DecodeEntities(text.Trim()) });
            index = nextTag;
        }

        while (stack.Count > 0)
        {
            var node = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            if (stack.Count > 0) stack[^1].Children.Add(node);
            else roots.Add(node);
        }

        return roots;
    }

    private static List<string> SplitTag(string value)
    {
        var matches = Regex.Matches(value, @"(?:[^\s""]+|""[^""]*"")+");
        return matches.Select(m => m.Value).ToList();
    }

    private static string PrefixToNamespace(string prefix)
    {
        if (prefix == "airsync") return "AirSync";
        if (prefix == "airsyncbase") return "AirSyncBase";
        if (prefix == "composemail") return "ComposeMail";
        if (prefix == "settings") return "Settings";
        return string.IsNullOrEmpty(prefix) ? prefix : char.ToUpperInvariant(prefix[0]) + prefix[1..];
    }

    private static string? ExtractXmlnsNamespace(string attributes)
    {
        var match = Regex.Match(attributes, @"xmlns(?::[\w-]+)?=""([^""]+):""", RegexOptions.IgnoreCase);
        return match.Success ? NormalizeNamespace(match.Groups[1].Value) : null;
    }

    private static string ReadInlineString(byte[] bytes, ref int offset)
    {
        var start = offset;
        while (offset < bytes.Length && bytes[offset] != 0x00) offset++;
        var value = Encoding.UTF8.GetString(bytes, start, offset - start);
        offset++;
        return value;
    }

    private static int ReadMultiByteInt(byte[] bytes, ref int offset)
    {
        var result = 0;
        byte current;
        do
        {
            current = bytes[offset++];
            result = (result << 7) | (current & 0x7f);
        } while ((current & 0x80) != 0);
        return result;
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string DecodeEntities(string value) =>
        value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
}
