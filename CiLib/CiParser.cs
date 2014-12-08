// CiParser.cs - Ci parser
//
// Copyright (C) 2011-2014  Piotr Fusik
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Foxoft.Ci {

  public partial class CiParser : CiLexer {
    SymbolTable Symbols;
    readonly List<CiConst> ConstArrays = new List<CiConst>();
    public CiClass CurrentClass = null;
    public CiMethod CurrentMethod = null;

    public CiParser() {
      SymbolTable globals = new SymbolTable(null);
      globals.Add(CiBoolType.Value);
      globals.Add(CiByteType.Value);
      globals.Add(CiIntType.Value);
      globals.Add(CiStringPtrType.Value);
      globals.Add(new CiConst(null, "true", CiBoolType.Value, true));
      globals.Add(new CiConst(null, "false", CiBoolType.Value, false));
      globals.Add(new CiConst(null, "null", CiType.Null, null));
      this.Symbols = new SymbolTable(globals);
    }

    string ParseId() {
      string id = this.CurrentString;
      Expect(CiToken.Id);
      return id;
    }

    CiCodeDoc ParseDoc() {
      if (See(CiToken.DocComment)) {
        CiDocParser parser = new CiDocParser(this);
        return parser.ParseCodeDoc();
      }
      return null;
    }

    CiEnum ParseEnum() {
      Expect(CiToken.Enum);
      string enumName = ParseId();
      CiEnum enu = new CiEnum(Here(), enumName);
      Expect(CiToken.LeftBrace);
      List<CiEnumValue> values = new List<CiEnumValue>();
      do {
        CiEnumValue value = new CiEnumValue(Here());
        value.Documentation = ParseDoc();
        value.Name = ParseId();
        value.Type = enu;
        values.Add(value);
      }
      while (Eat(CiToken.Comma));
      Expect(CiToken.RightBrace);
      enu.Values = values.ToArray();
      return enu;
    }

    CiType LookupType(string name) {
      CiSymbol symbol = this.Symbols.TryLookup(name);
      if (symbol is CiType) {
        return (CiType)symbol;
      }
      if (symbol is CiClass) {
        return new CiClassPtrType(Here(), name, (CiClass)symbol);
      }
      if (symbol == null) {
        CiType unknown = new CiUnknownType(Here(), name);
        return unknown;
      }
      throw new ParseException(Here(), "{0} is not a type", name);
    }

    CiType ParseArrayType(CiType baseType) {
      if (Eat(CiToken.LeftBracket)) {
        if (Eat(CiToken.RightBracket)) return new CiArrayPtrType(null, null, ParseArrayType(baseType));
        CiExpr len = ParseExpr();
        Expect(CiToken.RightBracket);
        return new CiArrayStorageType(null, null, ParseArrayType(baseType), len);
      }
      return baseType;
    }

    CiType ParseType() {
      string baseName = ParseId();
      CiType baseType;
      if (Eat(CiToken.LeftParenthesis)) {
        if (baseName == "string") {
          baseType = new CiStringStorageType(Here(), null) { LengthExpr = ParseExpr() };
          Expect(CiToken.RightParenthesis);
        }
        else {
          Expect(CiToken.RightParenthesis);
          baseType = new CiClassStorageType(Here(), baseName, new CiUnknownClass(Here(), baseName));
        }
      }
      else baseType = LookupType(baseName);
      return ParseArrayType(baseType);
    }

    object ParseConstInitializer(CiType type) {
      if (type is CiArrayType) {
        Expect(CiToken.LeftBrace);
        CiType elementType = ((CiArrayType)type).ElementType;
        List<object> list = new List<object>();
        if (!See(CiToken.RightBrace)) {
          do list.Add(ParseConstInitializer(elementType));
          while (Eat(CiToken.Comma));
        }
        Expect(CiToken.RightBrace);
        return list.ToArray();
      }
      return ParseExpr();
    }

    CiConst ParseConst() {
      Expect(CiToken.Const);
      CiType Type = ParseType();
      string Name = ParseId();
      Expect(CiToken.Assign);
      object Value = ParseConstInitializer(Type);
      Expect(CiToken.Semicolon);
      CiConst konst = new CiConst(Here(), Name, Type, Value);
      if (this.Symbols.Parent != null && konst.Type is CiArrayType) {
        this.ConstArrays.Add(konst);
        konst.GlobalName = "CiConstArray_" + this.ConstArrays.Count;
      }
      return konst;
    }

    CiBinaryResourceExpr ParseBinaryResource() {
      Expect(CiToken.LeftParenthesis);
      CiExpr nameExpr = ParseExpr();
      Expect(CiToken.RightParenthesis);
      return new CiBinaryResourceExpr { NameExpr = nameExpr };
    }

    CiExpr ParsePrimaryExpr() {
      if (See(CiToken.Increment) || See(CiToken.Decrement) || See(CiToken.Minus) || See(CiToken.Not)) {
        CiToken op = this.CurrentToken;
        NextToken();
        CiExpr inner = ParsePrimaryExpr();
        return new CiUnaryExpr { Op = op, Inner = inner };
      }
      if (Eat(CiToken.CondNot)) {
        CiExpr inner = ParsePrimaryExpr();
        return new CiCondNotExpr { Inner = inner };
      }
      CiExpr result;
      if (See(CiToken.IntConstant)) {
        result = new CiConstExpr(this.CurrentInt);
        NextToken();
      }
      else if (See(CiToken.StringConstant)) {
        result = new CiConstExpr(this.CurrentString);
        NextToken();
      }
      else if (Eat(CiToken.LeftParenthesis)) {
        result = ParseExpr();
        Expect(CiToken.RightParenthesis);
      }
      else if (See(CiToken.Id)) {
        string name = ParseId();
        if (name == "BinaryResource") result = ParseBinaryResource();
        else {
          CiSymbol symbol = this.Symbols.TryLookup(name);
          if (symbol is CiMacro) {
            Expand((CiMacro)symbol);
            Expect(CiToken.LeftParenthesis);
            result = ParseExpr();
            Expect(CiToken.RightParenthesis);
          }
          else {
            if (symbol == null) {
              symbol = new CiUnknownSymbol(Here(), name);
            }
            result = new CiSymbolAccess { Symbol = symbol };
          }
        }
      }
      else if (Eat(CiToken.New)) {
        CiType newType = ParseType();
        if (!(newType is CiClassStorageType || newType is CiArrayStorageType)) throw new ParseException(Here(), "'new' syntax error");
        result = new CiNewExpr { NewType = newType };
      }
      else throw new ParseException(Here(), "Invalid expression");
      for (;;) {
        if (Eat(CiToken.Dot)) result = new CiUnknownMemberAccess { Parent = result, Name = ParseId() };
        else if (Eat(CiToken.LeftParenthesis)) {
          CiMethodCall call = new CiMethodCall();
          call.Obj = result;
          List<CiExpr> args = new List<CiExpr>();
          if (!See(CiToken.RightParenthesis)) {
            do args.Add(ParseExpr());
            while (Eat(CiToken.Comma));
          }
          Expect(CiToken.RightParenthesis);
          call.Arguments = args.ToArray();
          result = call;
        }
        else if (Eat(CiToken.LeftBracket)) {
          CiExpr index = ParseExpr();
          Expect(CiToken.RightBracket);
          result = new CiIndexAccess { Parent = result, Index = index };
        }
        else if (See(CiToken.Increment) || See(CiToken.Decrement)) {
          CiToken op = this.CurrentToken;
          NextToken();
          return new CiPostfixExpr { Inner = result, Op = op };
        }
        else return result;
      }
    }

    CiExpr ParseMulExpr() {
      CiExpr left = ParsePrimaryExpr();
      while (See(CiToken.Asterisk) || See(CiToken.Slash) || See(CiToken.Mod)) {
        CiToken op = this.CurrentToken;
        NextToken();
        left = new CiBinaryExpr(left, op, ParsePrimaryExpr());
      }
      return left;
    }

    CiExpr ParseAddExpr() {
      CiExpr left = ParseMulExpr();
      while (See(CiToken.Plus) || See(CiToken.Minus)) {
        CiToken op = this.CurrentToken;
        NextToken();
        left = new CiBinaryExpr(left, op, ParseMulExpr());
      }
      return left;
    }

    CiExpr ParseShiftExpr() {
      CiExpr left = ParseAddExpr();
      while (See(CiToken.ShiftLeft) || See(CiToken.ShiftRight)) {
        CiToken op = this.CurrentToken;
        NextToken();
        left = new CiBinaryExpr(left, op, ParseAddExpr());
      }
      return left;
    }

    CiExpr ParseRelExpr() {
      CiExpr left = ParseShiftExpr();
      while (See(CiToken.Less) || See(CiToken.LessOrEqual) || See(CiToken.Greater) || See(CiToken.GreaterOrEqual)) {
        CiToken op = this.CurrentToken;
        NextToken();
        left = new CiBoolBinaryExpr(left, op, ParseShiftExpr());
      }
      return left;
    }

    CiExpr ParseEqualityExpr() {
      CiExpr left = ParseRelExpr();
      while (See(CiToken.Equal) || See(CiToken.NotEqual)) {
        CiToken op = this.CurrentToken;
        NextToken();
        left = new CiBoolBinaryExpr(left, op, ParseRelExpr());
      }
      return left;
    }

    CiExpr ParseAndExpr() {
      CiExpr left = ParseEqualityExpr();
      while (Eat(CiToken.And)) left = new CiBinaryExpr(left, CiToken.And, ParseEqualityExpr());
      return left;
    }

    CiExpr ParseXorExpr() {
      CiExpr left = ParseAndExpr();
      while (Eat(CiToken.Xor)) left = new CiBinaryExpr(left, CiToken.Xor, ParseAndExpr());
      return left;
    }

    CiExpr ParseOrExpr() {
      CiExpr left = ParseXorExpr();
      while (Eat(CiToken.Or)) left = new CiBinaryExpr(left, CiToken.Or, ParseXorExpr());
      return left;
    }

    CiExpr ParseCondAndExpr() {
      CiExpr left = ParseOrExpr();
      while (Eat(CiToken.CondAnd)) left = new CiBoolBinaryExpr(left, CiToken.CondAnd, ParseOrExpr());
      return left;
    }

    CiExpr ParseCondOrExpr() {
      CiExpr left = ParseCondAndExpr();
      while (Eat(CiToken.CondOr)) left = new CiBoolBinaryExpr(left, CiToken.CondOr, ParseCondAndExpr());
      return left;
    }

    CiExpr ParseExpr() {
      CiExpr left = ParseCondOrExpr();
      if (Eat(CiToken.QuestionMark)) {
        CiCondExpr result = new CiCondExpr();
        result.Cond = left;
        result.OnTrue = ParseExpr();
        Expect(CiToken.Colon);
        result.OnFalse = ParseExpr();
        return result;
      }
      return left;
    }

    CiMaybeAssign ParseMaybeAssign() {
      CiExpr left = ParseExpr();
      CiToken op = this.CurrentToken;
      if (op == CiToken.Assign || op == CiToken.AddAssign || op == CiToken.SubAssign || op == CiToken.MulAssign || op == CiToken.DivAssign || op == CiToken.ModAssign
          || op == CiToken.AndAssign || op == CiToken.OrAssign || op == CiToken.XorAssign || op == CiToken.ShiftLeftAssign || op == CiToken.ShiftRightAssign) {
        NextToken();
        CiAssign result = new CiAssign();
        result.Target = left;
        result.Op = op;
        result.Source = ParseMaybeAssign();
        return result;
      }
      return left;
    }

    ICiStatement ParseExprWithSideEffect() {
      ICiStatement result = ParseMaybeAssign() as ICiStatement;
      if (result == null) throw new ParseException(Here(), "Useless expression");
      return result;
    }

    CiExpr ParseCond() {
      Expect(CiToken.LeftParenthesis);
      CiExpr cond = ParseExpr();
      Expect(CiToken.RightParenthesis);
      return cond;
    }

    void OpenScope() {
      this.Symbols = new SymbolTable(this.Symbols);
    }

    void CloseScope() {
      this.Symbols = this.Symbols.Parent;
    }

    CiVar ParseVar() {
      CiVar def = new CiVar(Here(), null);
      def.Type = ParseType();
      def.Name = ParseId();
      if (Eat(CiToken.Assign)) def.InitialValue = ParseExpr();
      Expect(CiToken.Semicolon);
      this.Symbols.Add(def);
      return def;
    }

    ICiStatement ParseVarOrExpr() {
      string name = this.CurrentString;
      CiSymbol symbol = this.Symbols.TryLookup(name);
      if (symbol is CiMacro) {
        NextToken();
        Expand((CiMacro)symbol);
        return ParseStatement();
      }
      // try var
      StringBuilder sb = new StringBuilder();
      this.CopyTo = sb;
      try {
        return ParseVar();
      }
      catch (ParseException) {
      }
      finally {
        this.CopyTo = null;
      }

      // try expr
      this.CurrentString = name;
      this.CurrentToken = CiToken.Id;
      BeginExpand("ambigous code", sb.ToString(), null);
      SetReader(new StringReader(sb.ToString()));
      ICiStatement result = ParseExprWithSideEffect();
      Expect(CiToken.Semicolon);
      return result;
    }

    CiNativeBlock ParseNativeBlock() {
      StringBuilder sb = new StringBuilder();
      this.CopyTo = sb;
      try {
        Expect(CiToken.LeftBrace);
        int level = 1;
        for (;;) {
          if (See(CiToken.EndOfFile)) throw new ParseException(Here(), "Native block not terminated");
          if (See(CiToken.LeftBrace)) level++;
          else if (See(CiToken.RightBrace)) if (--level == 0) break;
          NextToken();
        }
      }
      finally {
        this.CopyTo = null;
      }
      NextToken();
      Trace.Assert(sb[sb.Length - 1] == '}');
      sb.Length--;
      return new CiNativeBlock { Content = sb.ToString() };
    }

    CiReturn ParseReturn() {
      CiReturn result = new CiReturn();
      if (this.CurrentMethod.Signature.ReturnType != CiType.Void) {
        result.Value = ParseExpr();
      }
      Expect(CiToken.Semicolon);
      return result;
    }

    CiSwitch ParseSwitch() {
      Expect(CiToken.LeftParenthesis);
      CiSwitch result = new CiSwitch();
      result.Value = ParseExpr();
      Expect(CiToken.RightParenthesis);
      Expect(CiToken.LeftBrace);

      List<CiCase> cases = new List<CiCase>();
      while (Eat(CiToken.Case)) {
        List<object> values = new List<object>();
        do {
          values.Add(ParseExpr());
          Expect(CiToken.Colon);
        }
        while (Eat(CiToken.Case));
        if (See(CiToken.Default)) throw new ParseException(Here(), "Please remove case before default");
        CiCase kase = new CiCase { Values = values.ToArray() };

        List<ICiStatement> statements = new List<ICiStatement>();
        do statements.Add(ParseStatement());
        while (!See(CiToken.Case) && !See(CiToken.Default) && !See(CiToken.Goto) && !See(CiToken.RightBrace));
        kase.Body = statements.ToArray();

        if (Eat(CiToken.Goto)) {
          if (Eat(CiToken.Case)) kase.FallthroughTo = ParseExpr();
          else if (Eat(CiToken.Default)) kase.FallthroughTo = null;
          else throw new ParseException(Here(), "Expected goto case or goto default");
          Expect(CiToken.Semicolon);
          kase.Fallthrough = true;
        }
        cases.Add(kase);
      }
      if (cases.Count == 0) throw new ParseException(Here(), "Switch with no cases");
      result.Cases = cases.ToArray();

      if (Eat(CiToken.Default)) {
        Expect(CiToken.Colon);
        List<ICiStatement> statements = new List<ICiStatement>();
        do statements.Add(ParseStatement());
        while (!See(CiToken.RightBrace));
        result.DefaultBody = statements.ToArray();
      }

      Expect(CiToken.RightBrace);
      return result;
    }

    ICiStatement ParseStatement() {
      while (Eat(CiToken.Macro)) {
        this.Symbols.Add(ParseMacro());
      }
      if (See(CiToken.Id)) {
        return ParseVarOrExpr();
      }
      if (See(CiToken.LeftBrace)) {
        OpenScope();
        CiBlock result = ParseBlock();
        CloseScope();
        return result;
      }
      if (Eat(CiToken.Break)) {
        Expect(CiToken.Semicolon);
        return new CiBreak();
      }
      if (See(CiToken.Const)) {
        CiConst konst = ParseConst();
        this.Symbols.Add(konst);
        return konst;
      }
      if (Eat(CiToken.Continue)) {
        Expect(CiToken.Semicolon);
        return new CiContinue();
      }
      if (Eat(CiToken.Delete)) {
        CiExpr expr = ParseExpr();
        Expect(CiToken.Semicolon);
        return new CiDelete { Expr = expr };
      }
      if (Eat(CiToken.Do)) {
        CiDoWhile result = new CiDoWhile();
        result.Body = ParseStatement();
        Expect(CiToken.While);
        result.Cond = ParseCond();
        Expect(CiToken.Semicolon);
        return result;
      }
      if (Eat(CiToken.For)) {
        Expect(CiToken.LeftParenthesis);
        OpenScope();
        CiFor result = new CiFor();
        if (See(CiToken.Id)) {
          result.Init = ParseVarOrExpr();
        }
        else Expect(CiToken.Semicolon);
        if (!See(CiToken.Semicolon)) {
          result.Cond = ParseExpr();
        }
        Expect(CiToken.Semicolon);
        if (!See(CiToken.RightParenthesis)) {
          result.Advance = ParseExprWithSideEffect();
        }
        Expect(CiToken.RightParenthesis);
        result.Body = ParseStatement();
        CloseScope();
        return result;
      }
      if (Eat(CiToken.If)) {
        CiIf result = new CiIf();
        result.Cond = ParseCond();
        result.OnTrue = ParseStatement();
        if (Eat(CiToken.Else)) {
          result.OnFalse = ParseStatement();
        }
        return result;
      }
      if (Eat(CiToken.Native)) {
        return ParseNativeBlock();
      }
      if (Eat(CiToken.Return)) {
        return ParseReturn();
      }
      if (Eat(CiToken.Switch)) {
        return ParseSwitch();
      }
      if (Eat(CiToken.Throw)) {
        CiThrow result = new CiThrow();
        result.Message = ParseExpr();
        Expect(CiToken.Semicolon);
        return result;
      }
      if (Eat(CiToken.While)) {
        CiWhile result = new CiWhile();
        result.Cond = ParseCond();
        result.Body = ParseStatement();
        return result;
      }
      throw new ParseException(Here(), "Invalid statement");
    }

    CiBlock ParseBlock() {
      Expect(CiToken.LeftBrace);
      List<ICiStatement> statements = new List<ICiStatement>();
      while (!Eat(CiToken.RightBrace)) {
        statements.Add(ParseStatement());
      }
      return new CiBlock(statements.ToArray());
    }

    CiParam CreateThis() {
      CiType Type = new CiClassPtrType(Here(), this.CurrentClass.Name, this.CurrentClass);
      CiParam thiz = new CiParam(Here(), "this", Type);
      this.Symbols.Add(thiz);
      return thiz;
    }

    CiType ParseReturnType() {
      if (Eat(CiToken.Void)) return CiType.Void;
      return ParseType();
    }

    CiParam[] ParseParams() {
      Expect(CiToken.LeftParenthesis);
      List<CiParam> paramz = new List<CiParam>();
      if (!See(CiToken.RightParenthesis)) {
        do {
          CiCodeDoc Docs = ParseDoc();
          CiType Type = ParseType();
          string Name = ParseId();
          CiParam param = new CiParam(Here(), Name, Type, Docs);
          this.Symbols.Add(param);
          paramz.Add(param);
        }
        while (Eat(CiToken.Comma));
      }
      Expect(CiToken.RightParenthesis);
      return paramz.ToArray();
    }

    void ParseMethod(CiMethod method) {
      this.CurrentMethod = method;
      OpenScope();
      if (method.CallType != CiCallType.Static) {
        method.This = CreateThis();
      }
      method.Signature.Params = ParseParams();
      if (method.CallType == CiCallType.Abstract) {
        Expect(CiToken.Semicolon);
      }
      else if (method.Signature.ReturnType != CiType.Void && Eat(CiToken.Return)) {
        method.Body = ParseReturn();
      }
      else {
        method.Body = ParseBlock();
      }
      CloseScope();
      this.CurrentMethod = null;
    }

    CiMethod ParseConstructor() {
      OpenScope();
      CiMethod method = new CiMethod(Here(), "<constructor>", CiType.Void) { Class = this.CurrentClass, CallType = CiCallType.Normal, This = CreateThis() };
      this.CurrentMethod = method;
      method.Body = ParseBlock();
      CloseScope();
      this.CurrentMethod = null;
      return method;
    }

    CiClass ParseClass() {
      CiClass klass = new CiClass(Here(), null);
      klass.SourceFilename = this.SourceFilename;
      if (Eat(CiToken.Abstract)) {
        klass.IsAbstract = true;
      }
      Expect(CiToken.Class);
      klass.Name = ParseId();
      if (Eat(CiToken.Colon)) {
        klass.BaseClass = new CiUnknownClass(Here(), ParseId());
      }
      Expect(CiToken.LeftBrace);
      OpenScope();
      this.CurrentClass = klass;
      klass.Members = this.Symbols;
      while (!Eat(CiToken.RightBrace)) {
        CiCodeDoc doc = ParseDoc();
        CiVisibility visibility = CiVisibility.Private;
        if (Eat(CiToken.Public)) visibility = CiVisibility.Public;
        else if (Eat(CiToken.Internal)) visibility = CiVisibility.Internal;
        CiSymbol symbol;
        if (See(CiToken.Const)) {
          symbol = ParseConst();
          ((CiConst)symbol).Class = klass;
        }
        else if (Eat(CiToken.Macro)) {
          if (visibility != CiVisibility.Private) throw new ParseException(Here(), "Macros must be private");
          symbol = ParseMacro();
        }
        else {
          CiCallType callType;
          if (Eat(CiToken.Static)) callType = CiCallType.Static;
          else if (Eat(CiToken.Abstract)) {
            if (!klass.IsAbstract) throw new ParseException(Here(), "Abstract methods only allowed in abstract classes");
            callType = CiCallType.Abstract;
            if (visibility == CiVisibility.Private) visibility = CiVisibility.Internal;
          }
          else if (Eat(CiToken.Virtual)) {
            callType = CiCallType.Virtual;
            if (visibility == CiVisibility.Private) visibility = CiVisibility.Internal;
          }
          else if (Eat(CiToken.Override)) {
            callType = CiCallType.Override;
            if (visibility == CiVisibility.Private) visibility = CiVisibility.Internal;
          }
          else callType = CiCallType.Normal;
          CiType type = ParseReturnType();
          if (type is CiClassStorageType && See(CiToken.LeftBrace)) {
            if (type.Name != klass.Name) {
              throw  new ParseException(Here(), "{0}() looks like a constructor, but it is in a different class {1}", type.Name, klass.Name);
            }
            if (callType != CiCallType.Normal) {
              throw new ParseException(Here(), "Constructor cannot be static, virtual or override");
            }
            if (klass.Constructor != null) {
              throw new ParseException(Here(), "Duplicate constructor");
            }
            klass.Constructor = ParseConstructor();
            continue;
          }
          string name = ParseId();
          if (See(CiToken.LeftParenthesis)) {
            CiMethod method = new CiMethod(Here(), name, type) { Class = klass, CallType = callType };
            ParseMethod(method);
            symbol = method;
          }
          else {
            if (visibility != CiVisibility.Private) throw new ParseException(Here(), "Fields must be private");
            if (callType != CiCallType.Normal) throw new ParseException(Here(), "Fields cannot be static, abstract, virtual or override");
            if (type == CiType.Void) throw new ParseException(Here(), "Field is void");
            Expect(CiToken.Semicolon);
            symbol = new CiField(Here(), name, klass, type);
          }
        }
        symbol.Documentation = doc;
        symbol.Visibility = visibility;
        klass.Members.Add(symbol);
      }
      this.CurrentClass = null;
      CloseScope();
      klass.ConstArrays = this.ConstArrays.ToArray();
      this.ConstArrays.Clear();
      return klass;
    }

    CiDelegate ParseDelegate() {
      CiDelegate del = new CiDelegate(Here(), null);
      Expect(CiToken.Delegate);
      del.ReturnType = ParseReturnType();
      del.Name = ParseId();
      OpenScope();
      del.Params = ParseParams();
      CloseScope();
      Expect(CiToken.Semicolon);
      return del;
    }

    CiComment CurrentComment = null;

    public override CiToken NextToken() {
      CiToken token = ReadToken();
      while (token == CiToken.Comment) {
        if (CurrentComment == null) {
          CurrentComment = new CiComment();
        }
        CurrentComment.Add(CurrentString);
        token = ReadToken();
      }
      this.CurrentToken = token;
      return token;
    }

    public bool HasComments() {
      return CurrentComment != null ? CurrentComment.Comments.Count > 0 : false;
    }

    public CiComment GlobalComment = null;

    void MergeComments(ref CiComment master, CiComment newComments) {
      if (master == null) {
        master = new CiComment(newComments.Comments, true);
      }
      else {
        bool equal = false;
        if (newComments.Comments.Count == master.Comments.Count) {
          for (int i = 0; i < newComments.Comments.Count - 1; i++) {
            string c1 = newComments.Comments[i];
            string c2 = master.Comments[i];
            if (!(String.IsNullOrEmpty(c1) && String.IsNullOrEmpty(c2))) {
              break;
            }
            if (!c1.Equals(c2)) {
              break;
            }
          }
          equal = true;
        }
        if (!equal) {
          master.Comments.AddRange(newComments.Comments);
        }
      }
    }

    public void Parse(string filename, TextReader reader) {
      Open(filename, reader);
      bool hasComment = false;
      while (!See(CiToken.EndOfFile)) {
        if (!hasComment) {
          if (HasComments()) {
            hasComment = true;
            MergeComments(ref GlobalComment, CurrentComment);
            CurrentComment = null;
          }
        }
        CiCodeDoc doc = ParseDoc();
        bool pub = Eat(CiToken.Public);
        CiSymbol symbol;
        if (See(CiToken.Enum)) {
          symbol = ParseEnum();
        }
        else if (See(CiToken.Class) || See(CiToken.Abstract)) {
          symbol = ParseClass();
        }
        else if (See(CiToken.Delegate)) {
          symbol = ParseDelegate();
        }
        else {
          throw new ParseException(Here(), "Expected class, enum or delegate");
        }
        symbol.Documentation = doc;
        symbol.Visibility = pub ? CiVisibility.Public : CiVisibility.Internal;
        this.Symbols.Add(symbol);
      }
      if (!hasComment && HasComments()) {
        GlobalComment = CurrentComment;
      }
      CurrentComment = null;
    }

    public CiProgram Program {
      get {
        return new CiProgram(this.GlobalComment, this.Symbols);
      }
    }
  }
}
