using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealPolitic.Core;

public static partial class TinyJson
{
    public sealed partial class Writer
    {
        public Writer Value(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value))throw new ArgumentOutOfRangeException(nameof(value));
            FlushPending();_sb.Append(value.ToString("R",CultureInfo.InvariantCulture));return this;
        }
    }
    /// <summary>New persisted procedures use strict JSON. Legacy Parse keeps its old semantics.</summary>
    public static Node ParseStrict(string json)
    {
        if(json==null)throw new ArgumentNullException(nameof(json));
        if(json.Length>32*1024*1024)throw new FormatException("JSON exceeds the state size limit.");
        var scanner=new StrictScanner(json);scanner.Value(0);scanner.White();
        if(scanner.Position!=json.Length)throw new FormatException("Trailing data in JSON.");
        return Parse(json);
    }
    private sealed class StrictScanner
    {
        private readonly string _s;
        public int Position;
        public StrictScanner(string s){_s=s;}
        public void White(){while(Position<_s.Length && (_s[Position]==' '||_s[Position]=='\t'||_s[Position]=='\n'||_s[Position]=='\r'))Position++;}
        private bool Take(char c){White();if(Position<_s.Length&&_s[Position]==c){Position++;return true;}return false;}
        private void Need(char c){if(!Take(c))throw new FormatException("Expected JSON token "+c);}
        private void Word(string word){if(Position+word.Length>_s.Length || string.CompareOrdinal(_s,Position,word,0,word.Length)!=0)throw new FormatException("Invalid JSON literal.");Position+=word.Length;}
        public void Value(int depth)
        {
            if(depth>64)throw new FormatException("JSON nesting exceeds the limit.");
            White();if(Position>=_s.Length)throw new FormatException("Truncated JSON.");
            switch(_s[Position])
            {
                case '{':
                    Position++;if(Take('}'))return;var keys=new HashSet<string>(StringComparer.Ordinal);
                    do{White();string key=String();if(!keys.Add(key))throw new FormatException("Duplicate JSON key.");Need(':');Value(depth+1);}while(Take(','));
                    Need('}');return;
                case '[':
                    Position++;if(Take(']'))return;do{Value(depth+1);}while(Take(','));Need(']');return;
                case '"':String();return;
                case 't':Word("true");return;
                case 'f':Word("false");return;
                case 'n':Word("null");return;
                default:Number();return;
            }
        }
        private string String()
        {
            if(Position>=_s.Length||_s[Position]!='"')throw new FormatException("Expected JSON string.");
            int start=Position++;
            while(Position<_s.Length)
            {
                char c=_s[Position++];
                if(c=='"'){int decode=start;return ParseString(_s,ref decode);}
                if(c<32)throw new FormatException("Control character in JSON string.");
                if(c!='\\')continue;
                if(Position>=_s.Length)throw new FormatException("Truncated JSON escape.");
                char escape=_s[Position++];
                if(escape=='u')
                {
                    if(Position+4>_s.Length)throw new FormatException("Truncated Unicode escape.");
                    for(int k=0;k<4;k++){char h=_s[Position++];if(!(h>='0'&&h<='9'||h>='a'&&h<='f'||h>='A'&&h<='F'))throw new FormatException("Invalid Unicode escape.");}
                }
                else if("\"\\/bfnrt".IndexOf(escape)<0)throw new FormatException("Invalid JSON escape.");
            }
            throw new FormatException("Unterminated JSON string.");
        }
        private bool Digit()=>Position<_s.Length&&_s[Position]>='0'&&_s[Position]<='9';
        private void Number()
        {
            int start=Position;
            if(Position<_s.Length&&_s[Position]=='-')Position++;
            if(!Digit())throw new FormatException("Invalid JSON number.");
            if(_s[Position]=='0')Position++;else while(Digit())Position++;
            if(Position<_s.Length&&_s[Position]=='.'){Position++;if(!Digit())throw new FormatException("Invalid JSON fraction.");while(Digit())Position++;}
            if(Position<_s.Length&&(_s[Position]=='e'||_s[Position]=='E'))
            {Position++;if(Position<_s.Length&&(_s[Position]=='+'||_s[Position]=='-'))Position++;if(!Digit())throw new FormatException("Invalid JSON exponent.");while(Digit())Position++;}
            if(!double.TryParse(_s.Substring(start,Position-start),NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||double.IsNaN(value)||double.IsInfinity(value))throw new FormatException("Non-finite JSON number.");
        }
    }
}
