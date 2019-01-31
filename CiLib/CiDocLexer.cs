// CiDocLexer.cs - Ci documentation lexer
//
// Copyright (C) 2011  Piotr Fusik
// Copyright (C) 2013-2014  Enrico Croce
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

namespace Foxoft.Ci {

  public enum CiDocToken {
    EndOfFile,
    Char,
    CodeDelimiter,
    Bullet,
    Para,
    Period
  }

  public class CiDocLexer {
    readonly protected CiLexer ciLexer;
    bool CheckPeriod;
    public CiDocToken CurrentToken;
    public int CurrentChar = CiLexer.SPECIAL_CR;

    public CiDocLexer(CiLexer lexer) {
      this.ciLexer = lexer;
      this.CheckPeriod = true;
      NextToken();
    }

    int PeekChar() {
      return this.ciLexer.PeekChar();
    }

    int ReadChar() {
      int c = this.ciLexer.ReadChar();
      if (c == CiLexer.SPECIAL_CR && this.ciLexer.NextToken() != CiToken.DocComment) return -1;
      return c;
    }

    CiDocToken ReadToken() {
      int lastChar = this.CurrentChar;
      for (;;) {
        int c = ReadChar();
        this.CurrentChar = c;
        switch (c) {
          case -1:
            return CiDocToken.EndOfFile;
          case '`':
            return CiDocToken.CodeDelimiter;
          case '*':
            if (lastChar == CiLexer.SPECIAL_CR && PeekChar() == CiLexer.SPECIAL_SPACE) {
              ReadChar();
              return CiDocToken.Bullet;
            }
            return CiDocToken.Char;
          case CiLexer.SPECIAL_CR:
            if (this.CheckPeriod && lastChar == '.') {
              this.CheckPeriod = false;
              return CiDocToken.Period;
            }
            if (lastChar == CiLexer.SPECIAL_CR) return CiDocToken.Para;
            return CiDocToken.Char;
          default:
            return CiDocToken.Char;
        }
      }
    }

    public CiDocToken NextToken() {
      CiDocToken token = ReadToken();
      this.CurrentToken = token;
      return token;
    }
  }
}
