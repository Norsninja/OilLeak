using System;
using System.Collections.Generic;
using System.Text;

namespace OilLeak.Toast.Data
{
    public class ToastContentLoader
    {
        private string json;
        private int position;
        private int line;
        private int column;

        public (ToastVoiceData data, ValidationResult result) Parse(string jsonContent)
        {
            var result = new ValidationResult();

            if (string.IsNullOrEmpty(jsonContent))
            {
                result.AddError("JSON content is empty or null");
                return (null, result);
            }

            json = jsonContent;
            position = 0;
            line = 1;
            column = 1;

            try
            {
                SkipWhitespace();
                var data = ParseToastVoiceData();

                // Validate the parsed data
                ValidateToastData(data, result);

                return (data, result);
            }
            catch (Exception ex)
            {
                result.AddError($"Parse error: {ex.Message}", line, column);
                return (null, result);
            }
        }

        private ToastVoiceData ParseToastVoiceData()
        {
            var data = new ToastVoiceData();
            ExpectChar('{');

            while (!IsAtChar('}'))
            {
                var key = ParseString();
                ExpectChar(':');

                switch (key)
                {
                    case "version":
                        data.version = ParseString();
                        break;
                    case "voiceId":
                        data.voiceId = ParseString();
                        break;
                    case "defaultHandle":
                        data.defaultHandle = ParseString();
                        break;
                    case "defaultIconKey":
                        data.defaultIconKey = ParseString();
                        break;
                    case "defaultSoundKey":
                        data.defaultSoundKey = ParseString();
                        break;
                    case "acts":
                        data.acts = ParseActs();
                        break;
                    default:
                        SkipValue(); // Unknown field, skip it
                        break;
                }

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar('}');
            return data;
        }

        private Dictionary<string, ActData> ParseActs()
        {
            var acts = new Dictionary<string, ActData>();
            ExpectChar('{');

            while (!IsAtChar('}'))
            {
                var actName = ParseString();
                ExpectChar(':');
                acts[actName] = ParseActData();

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar('}');
            return acts;
        }

        private ActData ParseActData()
        {
            var act = new ActData();
            ExpectChar('{');

            while (!IsAtChar('}'))
            {
                var key = ParseString();
                ExpectChar(':');

                switch (key)
                {
                    case "integrityRange":
                        act.integrityRange = ParseFloatArray();
                        break;
                    case "messages":
                        act.messages = ParseMessages();
                        break;
                    default:
                        SkipValue();
                        break;
                }

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar('}');
            return act;
        }

        private List<ToastMessage> ParseMessages()
        {
            var messages = new List<ToastMessage>();
            ExpectChar('[');

            while (!IsAtChar(']'))
            {
                messages.Add(ParseMessage());

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar(']');
            return messages;
        }

        private ToastMessage ParseMessage()
        {
            var message = new ToastMessage();
            ExpectChar('{');

            while (!IsAtChar('}'))
            {
                var key = ParseString();
                ExpectChar(':');

                switch (key)
                {
                    case "id":
                        message.id = ParseString();
                        break;
                    case "triggerId":
                        message.triggerId = ParseString();
                        break;
                    case "template":
                        message.template = ParseString();
                        break;
                    case "weight":
                        message.weight = ParseFloat();
                        break;
                    case "prerequisites":
                        message.prerequisites = ParsePrerequisites();
                        break;
                    case "handleOverride":
                        message.handleOverride = ParseStringOrNull();
                        break;
                    case "iconKey":
                        message.iconKey = ParseStringOrNull();
                        break;
                    case "soundKey":
                        message.soundKey = ParseStringOrNull();
                        break;
                    default:
                        SkipValue();
                        break;
                }

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar('}');
            return message;
        }

        private List<PrerequisiteCondition> ParsePrerequisites()
        {
            var prereqs = new List<PrerequisiteCondition>();
            ExpectChar('[');

            while (!IsAtChar(']'))
            {
                if (IsAtChar('{'))
                {
                    prereqs.Add(ParsePrerequisite());
                }

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar(']');
            return prereqs;
        }

        private PrerequisiteCondition ParsePrerequisite()
        {
            var prereq = new PrerequisiteCondition();
            ExpectChar('{');

            while (!IsAtChar('}'))
            {
                var key = ParseString();
                ExpectChar(':');

                switch (key)
                {
                    case "type":
                        prereq.type = ParseString();
                        break;
                    case "op":
                        prereq.op = ParseString();
                        break;
                    case "value":
                        prereq.value = ParseFloat();
                        break;
                    default:
                        SkipValue();
                        break;
                }

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar('}');
            return prereq;
        }

        private float[] ParseFloatArray()
        {
            var floats = new List<float>();
            ExpectChar('[');

            while (!IsAtChar(']'))
            {
                floats.Add(ParseFloat());

                if (IsAtChar(','))
                {
                    ExpectChar(',');
                    SkipWhitespace();
                }
            }

            ExpectChar(']');
            return floats.ToArray();
        }

        private float ParseFloat()
        {
            SkipWhitespace();
            var start = position;

            if (CurrentChar() == '-')
                Advance();

            while (char.IsDigit(CurrentChar()) || CurrentChar() == '.')
                Advance();

            var numStr = json.Substring(start, position - start);
            if (float.TryParse(numStr, out float result))
                return result;

            throw new Exception($"Invalid number: {numStr}");
        }

        private string ParseString()
        {
            SkipWhitespace();
            ExpectChar('"');
            var sb = new StringBuilder();

            while (CurrentChar() != '"')
            {
                if (CurrentChar() == '\\')
                {
                    Advance();
                    switch (CurrentChar())
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(CurrentChar()); break;
                    }
                    Advance();
                }
                else
                {
                    sb.Append(CurrentChar());
                    Advance();
                }
            }

            ExpectChar('"');
            return sb.ToString();
        }

        private string ParseStringOrNull()
        {
            SkipWhitespace();
            if (json.Substring(position, Math.Min(4, json.Length - position)) == "null")
            {
                position += 4;
                column += 4;
                return null;
            }
            return ParseString();
        }

        private void SkipValue()
        {
            SkipWhitespace();
            var ch = CurrentChar();

            if (ch == '"')
            {
                ParseString();
            }
            else if (ch == '{')
            {
                int depth = 1;
                Advance();
                while (depth > 0)
                {
                    if (CurrentChar() == '{') depth++;
                    else if (CurrentChar() == '}') depth--;
                    Advance();
                }
            }
            else if (ch == '[')
            {
                int depth = 1;
                Advance();
                while (depth > 0)
                {
                    if (CurrentChar() == '[') depth++;
                    else if (CurrentChar() == ']') depth--;
                    Advance();
                }
            }
            else if (char.IsDigit(ch) || ch == '-')
            {
                ParseFloat();
            }
            else if (json.Substring(position, Math.Min(4, json.Length - position)) == "true")
            {
                position += 4;
                column += 4;
            }
            else if (json.Substring(position, Math.Min(5, json.Length - position)) == "false")
            {
                position += 5;
                column += 5;
            }
            else if (json.Substring(position, Math.Min(4, json.Length - position)) == "null")
            {
                position += 4;
                column += 4;
            }
        }

        private void SkipWhitespace()
        {
            while (position < json.Length && char.IsWhiteSpace(json[position]))
            {
                if (json[position] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
                position++;
            }
        }

        private char CurrentChar()
        {
            if (position >= json.Length)
                throw new Exception("Unexpected end of JSON");
            return json[position];
        }

        private void Advance()
        {
            if (json[position] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
            position++;
        }

        private bool IsAtChar(char ch)
        {
            SkipWhitespace();
            return position < json.Length && json[position] == ch;
        }

        private void ExpectChar(char ch)
        {
            SkipWhitespace();
            if (CurrentChar() != ch)
                throw new Exception($"Expected '{ch}' but found '{CurrentChar()}'");
            Advance();
            SkipWhitespace();
        }

        private void ValidateToastData(ToastVoiceData data, ValidationResult result)
        {
            if (data == null) return;

            // Check required fields
            if (string.IsNullOrEmpty(data.voiceId))
                result.AddError("Missing required field: voiceId");

            if (data.acts == null || data.acts.Count == 0)
                result.AddError("No acts defined");

            // Validate acts
            var messageIds = new HashSet<string>();
            foreach (var kvp in data.acts)
            {
                var act = kvp.Value;

                // Check integrity range
                if (act.integrityRange == null || act.integrityRange.Length != 2)
                {
                    result.AddError($"Act '{kvp.Key}' has invalid integrity range");
                }
                else if (act.integrityRange[0] > act.integrityRange[1])
                {
                    result.AddError($"Act '{kvp.Key}' has reversed integrity range");
                }

                // Check messages
                if (act.messages == null || act.messages.Count == 0)
                {
                    result.AddError($"Act '{kvp.Key}' has no messages");
                    continue;
                }

                foreach (var msg in act.messages)
                {
                    // Check for duplicate IDs
                    if (!string.IsNullOrEmpty(msg.id))
                    {
                        if (messageIds.Contains(msg.id))
                            result.AddError($"Duplicate message ID: {msg.id}");
                        else
                            messageIds.Add(msg.id);
                    }

                    // Check required message fields
                    if (string.IsNullOrEmpty(msg.triggerId))
                        result.AddError($"Message '{msg.id}' missing triggerId");

                    if (string.IsNullOrEmpty(msg.template))
                        result.AddError($"Message '{msg.id}' missing template");

                    if (msg.weight <= 0)
                        result.AddError($"Message '{msg.id}' has invalid weight: {msg.weight}");

                    // Validate template placeholders
                    ValidateTemplatePlaceholders(msg.template, msg.id, result);

                    // Validate prerequisites
                    if (msg.prerequisites != null)
                    {
                        foreach (var prereq in msg.prerequisites)
                        {
                            if (string.IsNullOrEmpty(prereq.type))
                                result.AddError($"Message '{msg.id}' has prerequisite with missing type");

                            if (string.IsNullOrEmpty(prereq.op))
                                result.AddError($"Message '{msg.id}' has prerequisite with missing operator");

                            if (!IsValidOperator(prereq.op))
                                result.AddError($"Message '{msg.id}' has invalid operator: {prereq.op}");
                        }
                    }
                }
            }
        }

        private void ValidateTemplatePlaceholders(string template, string messageId, ValidationResult result)
        {
            var validPlaceholders = new HashSet<string> { "{time}", "{blocked}", "{escaped}", "{integrity}" };
            var index = 0;

            while ((index = template.IndexOf('{', index)) != -1)
            {
                var endIndex = template.IndexOf('}', index);
                if (endIndex == -1)
                {
                    result.AddError($"Message '{messageId}' has unclosed placeholder");
                    break;
                }

                var placeholder = template.Substring(index, endIndex - index + 1);
                if (!validPlaceholders.Contains(placeholder))
                {
                    result.AddError($"Message '{messageId}' has invalid placeholder: {placeholder}");
                }

                index = endIndex + 1;
            }
        }

        private bool IsValidOperator(string op)
        {
            return op == ">=" || op == "<=" || op == "==" || op == "!=" || op == "<" || op == ">";
        }
    }
}