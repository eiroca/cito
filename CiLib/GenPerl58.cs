// GenPerl58.cs - Perl 5.8- code generator
//
// Copyright (C) 2013  Piotr Fusik
// Copyright (C) 2013-2019  Enrico Croce
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

using System.Diagnostics;
using System.Linq;

namespace Foxoft.Ci {

  public class GenPerl58 : GenPerl5 {
    public GenPerl58(string package) : base(package) {
    }

    public GenPerl58() : base() {
    }

    bool InEarlyBreakSwitch = false;

    public override void Statement_CiContinue(ICiStatement statement) {
      if (this.InEarlyBreakSwitch) {
        WriteLine("next LOOP;");
      }
      else {
        WriteLine("next;");
      }
    }

    static bool HasEarlyBreak(ICiStatement[] body) {
      return body.Any(stmt => HasBreak(stmt) && !(stmt is CiBreak));
    }

    static bool HasEarlyBreak(CiSwitch stmt) {
      if (stmt.Cases.Any(kase => HasEarlyBreak(kase.Body))) {
        return true;
      }
      if (stmt.DefaultBody != null && HasEarlyBreak(stmt.DefaultBody)) {
        return true;
      }
      return false;
    }

    static bool HasSwitchContinueAndEarlyBreak(ICiStatement stmt) {
      CiIf ifStmt = stmt as CiIf;
      if (ifStmt != null) {
        return HasSwitchContinueAndEarlyBreak(ifStmt.OnTrue) || HasSwitchContinueAndEarlyBreak(ifStmt.OnFalse);
      }
      CiBlock block = stmt as CiBlock;
      if (block != null) {
        return block.Statements.Any(s => HasSwitchContinueAndEarlyBreak(s));
      }
      CiSwitch switchStmt = stmt as CiSwitch;
      if (switchStmt != null) {
        return HasEarlyBreak(switchStmt) && HasContinue(switchStmt);
      }
      return false;
    }

    protected override void WriteLoopLabel(CiLoop stmt) {
      if (HasSwitchContinueAndEarlyBreak(stmt.Body)) {
        Write("LOOP: ");
      }
    }

    public override void Statement_CiSwitch(ICiStatement statement) {
      CiSwitch swich = (CiSwitch)statement;
      bool oldBreakDoWhile = this.BreakDoWhile;
      this.BreakDoWhile = false;
      bool hasEarlyBreak = HasEarlyBreak(swich);
      bool oldInEarlyBreakSwitch = this.InEarlyBreakSwitch;
      if (hasEarlyBreak) {
        this.InEarlyBreakSwitch = true;
        OpenBlock(); // block that "last" will exit
      }
      bool tmpVar = swich.Value.HasSideEffect;
      if (tmpVar) {
        Write("my $CISWITCH = ");
        Translate(swich.Value);
        WriteLine(";");
      }
      bool first = true;
      foreach (CiCase kase in swich.Cases) {
        if (!first) {
          Write("els");
        }
        Write("if (");
        first = true;
        // TODO: optimize ranges "case 1: case 2: case 3:"
        foreach (object value in kase.Values) {
          if (first) {
            first = false;
          }
          else {
            Write(" || ");
          }
          if (tmpVar) {
            Write("$CISWITCH");
          }
          else {
            WriteChild(CiPriority.Equality, swich.Value);
          }
          WriteFormat(" == {0}", DecodeValue(null, value));
        }
        Write(") ");
        OpenBlock();
        WriteCode(kase.Body, BodyLengthWithoutLastBreak(kase.Body));
        // TODO: fallthrough
        CloseBlock();
        Debug.Assert(!first);
      }
      if (swich.DefaultBody != null) {
        int length = BodyLengthWithoutLastBreak(swich.DefaultBody);
        if (length > 0) {
          Write("else ");
          OpenBlock();
          WriteCode(swich.DefaultBody, length);
          CloseBlock();
        }
      }
      if (hasEarlyBreak) {
        CloseBlock();
        this.InEarlyBreakSwitch = oldInEarlyBreakSwitch;
      }
      this.BreakDoWhile = oldBreakDoWhile;
    }
  }
}
