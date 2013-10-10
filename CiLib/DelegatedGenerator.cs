// DelegatedGenerator.cs - Base class for delegate-approach generators
//
// Copyright (C) 2013  Enrico Croce
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
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Foxoft.Ci {

  public delegate bool StatementAction(ICiStatement s);
  //
  public delegate void WriteSymbolDelegate(CiSymbol statement);
  public delegate void WriteStatementDelegate(ICiStatement statement);
  public delegate void WriteExprDelegate(CiExpr expr);
  public delegate void WriteUnaryOperatorDelegate(CiUnaryExpr expr,UnaryOperatorInfo token);
  public delegate void WriteBinaryOperatorDelegate(CiBinaryExpr expr,BinaryOperatorInfo token);
  //
  public delegate void WritePropertyAccessDelegate(CiPropertyAccess expr);
  public delegate void WriteMethodDelegate(CiMethodCall method);
  //
  public delegate string TranslateSymbolName(CiSymbol aSymbol);
  //
  public class OperatorInfo {
    public CiToken Token;
    public CiPriority Priority;
  }

  public class UnaryOperatorInfo: OperatorInfo {
    public string Prefix;
    public string Suffix;
    public WriteUnaryOperatorDelegate WriteDelegate;

    public UnaryOperatorInfo(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      this.Token = token;
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
      this.Prefix = prefix;
      this.Suffix = suffix;
    }
  }

  public class BinaryOperatorInfo: OperatorInfo {
    public bool ForcePar;
    public string Symbol;
    public WriteBinaryOperatorDelegate WriteDelegate;

    public BinaryOperatorInfo(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      this.ForcePar = true;
      if ((token == CiToken.Plus) || (token == CiToken.Minus) || (token == CiToken.Asterisk) || (token == CiToken.Slash)) {
        this.ForcePar = false;
      }
      this.Token = token;
      this.Priority = priority;
      this.Symbol = symbol;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class ExpressionInfo {
    public CiPriority Priority;
    public WriteExprDelegate WriteDelegate;

    public ExpressionInfo(CiPriority priority, WriteExprDelegate writeDelegate) {
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class UnaryOperatorMetadata {

    protected Dictionary<CiToken, UnaryOperatorInfo> Metadata = new Dictionary<CiToken, UnaryOperatorInfo>();

    public UnaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      UnaryOperatorInfo info = new UnaryOperatorInfo(token, priority, writeDelegate, prefix, suffix);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public UnaryOperatorInfo GetUnaryOperator(CiToken token) {
      UnaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class BinaryOperatorMetadata {

    protected Dictionary<CiToken, BinaryOperatorInfo> Metadata = new Dictionary<CiToken, BinaryOperatorInfo>();

    public BinaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      BinaryOperatorInfo info = new BinaryOperatorInfo(token, priority, writeDelegate, symbol);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public BinaryOperatorInfo GetBinaryOperator(CiToken token) {
      BinaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class ExpressionMetadata {
    protected Dictionary<Type, ExpressionInfo> Metadata = new Dictionary<Type, ExpressionInfo>();

    public ExpressionMetadata() {
    }

    public void Add(Type exprType, CiPriority priority, WriteExprDelegate writeDelegate) {
      ExpressionInfo info = new ExpressionInfo(priority, writeDelegate);
      if (!Metadata.ContainsKey(exprType)) {
        Metadata.Add(exprType, info);
      }
      else {
        Metadata[exprType] = info;
      }
    }

    public ExpressionInfo GetInfo(CiExpr expr) {
      ExpressionInfo result = null;
      Type exprType = expr.GetType();
      while (exprType!=null) {
        Metadata.TryGetValue(exprType, out result);
        if (result != null) {
          break;
        }
        exprType = exprType.BaseType;
      }
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + expr.GetType());
      }
      return result;
    }

    public virtual void Translate(CiExpr expr) {
      ExpressionInfo exprInfo = GetInfo(expr);
      if (exprInfo != null) {
        exprInfo.WriteDelegate(expr);
      }
      else {
        throw new ArgumentException(expr.ToString());
      }
    }
  }

  public class DelegateMappingMetadata<TYPE, DATA> where TYPE: class where DATA: class {
    public delegate void Delegator(DATA context);

    protected Dictionary<TYPE, Delegator> Metadata = new Dictionary<TYPE, Delegator>();

    public DelegateMappingMetadata() {
    }

    public void Add(TYPE typ, Delegator delegat) {
      if (!Metadata.ContainsKey(typ)) { 
        Metadata.Add(typ, delegat);
      }
      else {
        Metadata[typ] = delegat;
      }
    }

    public bool TryTranslate(TYPE typ, DATA context) {
      Delegator callDelegate = null;
      Metadata.TryGetValue(typ, out callDelegate);
      if (callDelegate != null) {
        callDelegate(context);
      }
      return (callDelegate != null);
    }
  }

  public class GenericMetadata<TYPE> : DelegateMappingMetadata<Type, TYPE> where TYPE: class {
    public void Translate(TYPE obj) {
      Type type = obj.GetType();
      bool translated = false;
      while (!translated) {
        translated = TryTranslate(type, obj);
        if (!translated) {
          type = type.BaseType;
          if (type == null) {
            throw new InvalidOperationException("No delegate for " + obj);
          }
        }
      }
    }
  }

  public class SymbolMapping {
    public TranslateSymbolName GetLocalName;
    public CiSymbol Symbol = null;
    public string NewName = "?";
    public SymbolMapping Parent = null;
    public List<SymbolMapping> childs = new List<SymbolMapping>();

    public SymbolMapping() {
    }

    public SymbolMapping(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck, TranslateSymbolName NameTranslator) {
      this.GetLocalName = NameTranslator;
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = this.GetUniqueName(inParentCheck);
    }

    public bool IsUnique(string baseName, bool inParentCheck) {
      SymbolMapping context = this.Parent;
      while (context!=null) {
        foreach (SymbolMapping item in context.childs) {
          if (String.Compare(item.NewName, baseName, true) == 0) {
            return false;
          }
        }
        if (inParentCheck) {
          context = context.Parent;
        }
        else {
          context = null;
        }
      }
      return true;
    }

    public string GetUniqueName(bool inParentCheck) {
      if (Symbol == null) {
        return "?";
      }
      string baseName = GetLocalName(Symbol);
      string curName = baseName;
      int suffix = 1;
      while (!IsUnique(curName, inParentCheck)) {
        curName = baseName + "__" + suffix;
        suffix++;
      }
      return curName;
    }
  }

  public abstract class DelegateGenerator : BaseGenerator {
    public DelegateGenerator(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public DelegateGenerator() : base() {
      InitMetadata();
      InitLibrary();
    }

    public override void Write(CiProgram prog) {
      PreProcess(prog);
      EmitProgram(prog);
    }

    public abstract void EmitProgram(CiProgram prog);
    #region Ci Language Translation
    protected virtual void InitMetadata() {
      InitSymbols();
      InitStatements();
      InitExpressions();
      InitOperators();
    }

    protected virtual void InitSymbols() {
      //TODO Use reflection to fill the structure
    }

    protected virtual void InitStatements() {
      //TODO Use reflection to fill the structure
    }

    protected virtual void InitExpressions() {
      //TODO Use reflection to fill the structure
    }

    protected virtual void InitOperators() {
      //TODO Use reflection to fill the structure
    }

    protected GenericMetadata<CiSymbol> Symbols = new GenericMetadata<CiSymbol>();
    protected GenericMetadata<ICiStatement> Statemets = new GenericMetadata<ICiStatement>();
    protected ExpressionMetadata Expressions = new ExpressionMetadata();
    protected BinaryOperatorMetadata BinaryOperators = new BinaryOperatorMetadata();
    protected UnaryOperatorMetadata UnaryOperators = new UnaryOperatorMetadata();

    protected void AddSymbolTranslator(Type symbol, GenericMetadata<CiSymbol>.Delegator delegat) {
      Symbols.Add(symbol, delegat);
    }

    protected void AddStatementTranslator(Type statemenent, GenericMetadata<ICiStatement>.Delegator delegat) {
      Statemets.Add(statemenent, delegat);
    }

    protected void Translate(CiExpr expr) {
      Expressions.Translate(expr);
    }

    protected void Translate(CiSymbol expr) {
      Symbols.Translate(expr);
    }

    protected void Translate(ICiStatement expr) {
      Statemets.Translate(expr);
    }
    #endregion
    #region Library Translation
    protected DelegateMappingMetadata<CiProperty, CiPropertyAccess> Properties = new DelegateMappingMetadata<CiProperty, CiPropertyAccess>();
    protected DelegateMappingMetadata<CiMethod, CiMethodCall> Methods = new DelegateMappingMetadata<CiMethod, CiMethodCall>();

    protected virtual void InitLibrary() {
      //TODO Use reflection to fill the structure
    }

    protected void AddPropertyTranslator(CiProperty prop, DelegateMappingMetadata<CiProperty, CiPropertyAccess>.Delegator del) {
      Properties.Add(prop, del);
    }

    protected void AddMethodTranslator(CiMethod met, DelegateMappingMetadata<CiMethod, CiMethodCall>.Delegator del) {
      Methods.Add(met, del);
    }

    protected bool Translate(CiPropertyAccess prop) {
      if (prop.Property != null) {
        return Properties.TryTranslate(prop.Property, prop);
      }
      else {
        return false;
      }
    }

    protected bool Translate(CiMethodCall call) {
      if (call.Method != null) {
        return Methods.TryTranslate(call.Method, call);
      }
      else {
        return false;
      }
    }
    #endregion
    #region Pre Processor
    protected virtual bool Execute(ICiStatement[] stmt, StatementAction action) {
      if (stmt != null) {
        foreach (ICiStatement s in stmt) {
          if (Execute(s, action)) {
            return true;
          }
        }
      }
      return false;
    }

    protected virtual bool Execute(ICiStatement stmt, StatementAction action) {
      if (stmt == null) {
        return false;
      }
      if (action(stmt)) {
        return true;
      }
      if (stmt is CiBlock) {
        if (Execute(((CiBlock)stmt).Statements, action)) {
          return true;
        }
      }
      else if (stmt is CiFor) {
        CiFor loop = (CiFor)stmt;
        if (Execute(loop.Init, action)) {
          return true;
        }
        if (Execute(loop.Body, action)) {
          return true;
        }
        if (Execute(loop.Advance, action)) {
          return true;
        }
      }
      else if (stmt is CiLoop) {
        CiLoop loop = (CiLoop)stmt;
        if (Execute(loop.Body, action)) {
          return true;
        }
      }
      else if (stmt is CiIf) {
        CiIf iiff = (CiIf)stmt;
        if (Execute(iiff.OnTrue, action)) {
          return true;
        }
        if (Execute(iiff.OnFalse, action)) {
          return true;
        }
      }
      else if (stmt is CiSwitch) {
        CiSwitch swith = (CiSwitch)stmt;
        foreach (CiCase cas in swith.Cases) {
          if (Execute(cas.Body, action)) {
            return true;
          }
        }
        if (Execute(swith.DefaultBody, action)) {
          return true;
        }
      }
      return false;
    }

    protected virtual void PreProcess(CiProgram program) {
      ResetSymbolMapping();
      TypeMapper.Reset();
      ClassOrder.Reset();
      SymbolMapping root = new SymbolMapping();
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiEnum) {
          AddSymbol(root, symbol);
        }
      }
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiDelegate) {
          AddSymbol(root, symbol);
        }
      }
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiClass) {
          ClassOrder.AddClass((CiClass)symbol);
        }
      }
      foreach (CiClass klass in ClassOrder.GetList()) {
        SymbolMapping parent = (klass.BaseClass != null ? Find(klass.BaseClass) : root);
        AddSymbol(parent, klass);
      }
      foreach (CiClass klass in ClassOrder.GetList()) {
        PreProcess(program, klass);
      }
    }

    protected virtual void PreProcess(CiProgram program, CiClass klass) {
      SymbolMapping parent = Find(klass);
      foreach (CiSymbol member in klass.Members) {
        if (member is CiField) {
          AddSymbol(parent, member);
          TypeMapper.AddType(((CiField)member).Type);
        }
      }
      foreach (CiConst konst in klass.ConstArrays) {
        AddSymbol(parent, konst);
        TypeMapper.AddType(konst.Type);
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        AddSymbol(parent, resource);
        TypeMapper.AddType(resource.Type);
      }
      if (klass.Constructor != null) {
        AddSymbol(parent, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          SymbolMapping methodContext = AddSymbol(parent, member, false);
          CiMethod method = (CiMethod)member;
          if (method.Signature.Params.Length > 0) {
            SymbolMapping methodCall = AddSymbol(methodContext, null);
            foreach (CiParam p in method.Signature.Params) {
              AddSymbol(methodCall, p);
              TypeMapper.AddType(p.Type);
            }
          }
          TypeMapper.AddType(method.Signature.ReturnType);
        }
      }
      if (klass.Constructor != null) {
        PreProcess(klass, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          PreProcess(klass, (CiMethod)member);
        }
      }
    }

    protected virtual void PreProcess(CiClass klass, CiMethod method) {
      Execute(method.Body, s => PreProcess(method, s));
    }

    protected virtual bool PreProcess(CiMethod method, ICiStatement stmt) {
      return false;
    }
    #endregion
    #region Helper
    protected static int ElemPerRow = 16;
    protected static string ElemSeparator = ", ";

    protected int GetArraySize(CiType type) {
      if (type is CiArrayStorageType) {
        CiArrayStorageType arr = (CiArrayStorageType)type;
        if (arr.LengthExpr == null) {
          return ((CiArrayStorageType)type).Length;
        }
      }
      return -1;
    }

    protected virtual string DecodeArray(CiType type, Array array) {
      StringBuilder res = new StringBuilder();
      if (array.Length >= ElemPerRow) {
        res.Append(NewLineStr);
        OpenBlock(false);
      }
      for (int i = 0; i < array.Length; i++) {
        res.Append(DecodeValue(type, array.GetValue(i)));
        if (i < (array.Length - 1)) {
          res.Append(ElemSeparator);
          if (i % ElemPerRow == 0) {
            res.Append(NewLineStr);
          }
        }
      }
      if (array.Length >= ElemPerRow) {
        CloseBlock(false);
        res.Append(NewLineStr);
      }
      return res.ToString();
    }

    protected virtual string DecodeValue(CiType type, object value) {
      return value.ToString();
    }

    protected string DecodeSymbol(CiSymbol var) {
      SymbolMapping symbol = Find(var);
      return (symbol != null) ? symbol.NewName : var.Name;
    }

    protected virtual void WriteChild(CiExpr parent, CiExpr child) {
      WriteChild(GetPriority(parent), child, false);
    }

    protected virtual void WriteChild(CiExpr parent, CiExpr child, bool nonAssoc) {
      WriteChild(GetPriority(parent), child, nonAssoc);
    }

    protected virtual void WriteChild(CiPriority parentPriority, CiExpr child) {
      WriteChild(parentPriority, child, false);
    }

    protected virtual void WriteChild(CiPriority parentPriority, CiExpr child, bool nonAssoc) {
      ExpressionInfo exprInfo = Expressions.GetInfo(child);
      if (exprInfo == null) {
        throw new ArgumentException(child.ToString());
      }
      if ((exprInfo.Priority < parentPriority) || (nonAssoc && (exprInfo.Priority == parentPriority))) {
        Write('(');
        exprInfo.WriteDelegate(child);
        Write(')');
      }
      else {
        exprInfo.WriteDelegate(child);
      }
    }

    protected virtual CiPriority GetPriority(CiExpr expr) {
      if (expr is CiCoercion) {
        return GetPriority((CiExpr)((CiCoercion)expr).Inner);
      }
      if (expr is CiBinaryExpr) {
        return BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr).Op).Priority;
      }
      ExpressionInfo exprInfo = Expressions.GetInfo(expr);
      if (exprInfo != null) {
        return exprInfo.Priority;
      }
      throw new ArgumentException(expr.GetType().Name);
    }
    #endregion
    #region Symbol Mapper
    //
    private HashSet<string> ReservedWords = null;
    private Dictionary<CiSymbol, SymbolMapping> varMap = new  Dictionary<CiSymbol, SymbolMapping>();
    //
    protected virtual string[] GetReservedWords() {
      return null;
    }

    protected virtual TranslateSymbolName GetSymbolNameTranslator() {
      return GetSymbolName;
    }

    public void ResetSymbolMapping() {
      string[] words = GetReservedWords();
      if (words != null) {
        ReservedWords = new HashSet<string>(words);
      }
      else {
        ReservedWords = new HashSet<string>();
      }
      varMap.Clear();
    }

    public bool HasSymbols() {
      return varMap.Count == 0;
    }

    public bool IsReservedWord(string aName) {
      return ReservedWords.Contains(aName.ToLower());
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol) {
      return AddSymbol(aParent, aSymbol, true);
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck) {
      SymbolMapping item = null;
      if (aSymbol != null) {
        item = new SymbolMapping(aParent, aSymbol, inParentCheck, GetSymbolNameTranslator());
        varMap.Add(aSymbol, item);
      }
      return item;
    }

    public SymbolMapping Find(CiSymbol symbol) {
      SymbolMapping result = null;
      varMap.TryGetValue(symbol, out result);
      return result;
    }

    public string GetSymbolName(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      StringBuilder tmpName = new StringBuilder(name.Length);
      foreach (char c in name) {
        tmpName.Append(CiLexer.IsLetter(c) ? c : '_');
      }
      string baseName = tmpName.ToString();
      if (IsReservedWord(baseName)) {
        baseName = "a" + baseName;
      }
      return baseName;
    }
    #endregion
  }
}