using System;
using System.Collections.Generic;

namespace OilLeak.Toast.Data
{
    [Serializable]
    public class ToastVoiceData
    {
        public string version;
        public string voiceId;
        public string defaultHandle;
        public string defaultIconKey;
        public string defaultSoundKey;
        public Dictionary<string, ActData> acts;
    }

    [Serializable]
    public class ActData
    {
        public float[] integrityRange;
        public List<ToastMessage> messages;
    }

    [Serializable]
    public class ToastMessage
    {
        public string id;
        public string triggerId;
        public string template;
        public float weight;
        public List<PrerequisiteCondition> prerequisites;
        public string handleOverride;
        public string iconKey;
        public string soundKey;
    }

    [Serializable]
    public class PrerequisiteCondition
    {
        public string type; // time_elapsed, integrity_below, items_degraded, etc.
        public string op;   // >=, <=, ==, !=, <, >
        public float value;
    }

    [Serializable]
    public class ValidationResult
    {
        public bool isValid;
        public List<ValidationError> errors;

        public ValidationResult()
        {
            isValid = true;
            errors = new List<ValidationError>();
        }

        public void AddError(string message, int line = -1, int column = -1)
        {
            isValid = false;
            errors.Add(new ValidationError { message = message, line = line, column = column });
        }
    }

    [Serializable]
    public class ValidationError
    {
        public string message;
        public int line;
        public int column;

        public override string ToString()
        {
            if (line >= 0 && column >= 0)
                return $"[Line {line}, Col {column}] {message}";
            else if (line >= 0)
                return $"[Line {line}] {message}";
            else
                return message;
        }
    }
}