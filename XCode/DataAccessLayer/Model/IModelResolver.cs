﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CSharp;
#if NET4
using System.Linq;
#else
using NewLife.Linq;
#endif
using XCode.Model;
using NewLife.Reflection;

namespace XCode.DataAccessLayer.Model
{
    /// <summary>模型解析器接口。解决名称大小写、去前缀、关键字等多个问题</summary>
    public interface IModelResolver
    {
        #region 名称处理
        /// <summary>获取别名。过滤特殊符号，过滤_之类的前缀。另外，避免一个表中的字段别名重名</summary>
        /// <param name="dc"></param>
        /// <returns></returns>
        String GetAlias(IDataColumn dc);

        /// <summary>获取别名。过滤特殊符号，过滤_之类的前缀。</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        String GetAlias(String name);

        /// <summary>去除前缀。默认去除第一个_前面部分，去除tbl和table前缀</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        String CutPrefix(String name);

        /// <summary>自动处理大小写</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        String FixWord(String name);

        ///// <summary>是否关键字</summary>
        ///// <param name="name"></param>
        ///// <returns></returns>
        //Boolean IsKeyWord(String name);

        /// <summary>获取显示名，如果描述不存在，则使用名称，否则使用描述前面部分，句号（中英文皆可）、换行分隔</summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        String GetDisplayName(String name, String description);
        #endregion

        #region 模型处理
        /// <summary>连接两个表。
        /// 实际上是猜测它们之间的关系，根据一个字段名是否等于另一个表的表名加某个字段名来判断是否存在关系。</summary>
        /// <param name="table"></param>
        /// <param name="rtable"></param>
        IDataTable Connect(IDataTable table, IDataTable rtable);

        /// <summary>猜测表间关系</summary>
        /// <param name="table"></param>
        /// <param name="rtable"></param>
        /// <param name="rname"></param>
        /// <param name="column"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        Boolean GuessRelation(IDataTable table, IDataTable rtable, String rname, IDataColumn column, String name);

        /// <summary>修正数据</summary>
        /// <param name="table"></param>
        IDataTable Fix(IDataTable table);

        /// <summary>修正数据列</summary>
        /// <param name="column"></param>
        IDataColumn Fix(IDataColumn column);
        #endregion
    }

    /// <summary>模型解析器。解决名称大小写、去前缀、关键字等多个问题</summary>
    public class ModelResolver : IModelResolver
    {
        #region 名称处理
        /// <summary>获取别名。过滤特殊符号，过滤_之类的前缀。另外，避免一个表中的字段别名重名</summary>
        /// <param name="dc"></param>
        /// <returns></returns>
        public virtual String GetAlias(IDataColumn dc)
        {
            var name = GetAlias(dc.Name);
            if (dc.Table != null)
            {
                var lastname = name;
                var index = 0;
                var cs = dc.Table.Columns;
                for (int i = 0; i < cs.Count; i++)
                {
                    var item = cs[i];
                    if (item != dc && item.Name != dc.Name)
                    {
                        if (lastname.EqualIgnoreCase(item.Alias))
                        {
                            lastname = name + ++index;
                            // 从头开始
                            i = -1;
                        }
                    }
                }
                lastname = name;
            }
            return name;
        }

        /// <summary>获取别名。过滤特殊符号，过滤_之类的前缀。</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual String GetAlias(String name)
        {
            if (String.IsNullOrEmpty(name)) return name;

            name = name.Replace("$", null);
            name = name.Replace("(", null);
            name = name.Replace(")", null);
            name = name.Replace("（", null);
            name = name.Replace("）", null);
            name = name.Replace(" ", null);
            name = name.Replace("　", null);

            // 很多时候，这个别名就是表名
            return FixWord(CutPrefix(name));
        }

        /// <summary>去除前缀。默认去除第一个_前面部分，去除tbl和table前缀</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual String CutPrefix(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;

            // 自动去掉前缀
            Int32 n = name.IndexOf("_");
            // _后至少要有2个字母，并且后一个不能是_
            if (n >= 0 && n < name.Length - 2 && name[n + 1] != '_')
            {
                String str = name.Substring(n + 1);
                if (!IsKeyWord(str)) name = str;
            }

            String[] ss = new String[] { "tbl", "table" };
            foreach (String s in ss)
            {
                if (name.StartsWith(s))
                {
                    String str = name.Substring(s.Length);
                    if (!IsKeyWord(str)) name = str;
                }
                else if (name.EndsWith(s))
                {
                    String str = name.Substring(0, name.Length - s.Length);
                    if (!IsKeyWord(str)) name = str;
                }
            }

            return name;
        }

        /// <summary>自动处理大小写</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual String FixWord(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;

            if (name.Equals("ID", StringComparison.OrdinalIgnoreCase)) return "ID";

            if (name.Length <= 2) return name;

            Int32 lowerCount = 0;
            Int32 upperCount = 0;
            foreach (var item in name)
            {
                if (item >= 'a' && item <= 'z')
                    lowerCount++;
                else if (item >= 'A' && item <= 'Z')
                    upperCount++;
            }

            //没有或者只有一个小写字母的，需要修正
            //没有大写的，也要修正
            if (lowerCount <= 1 || upperCount < 1)
            {
                name = name.ToLower();
                Char c = name[0];
                if (c >= 'a' && c <= 'z') c = (Char)(c - 'a' + 'A');
                name = c + name.Substring(1);
            }

            //处理Is开头的，第三个字母要大写
            if (name.StartsWith("Is") && name.Length >= 3)
            {
                Char c = name[2];
                if (c >= 'a' && c <= 'z')
                {
                    c = (Char)(c - 'a' + 'A');
                    name = name.Substring(0, 2) + c + name.Substring(3);
                }
            }

            return name;
        }

        /// <summary>代码生成器</summary>
        private static CSharpCodeProvider _CG = new CSharpCodeProvider();

        /// <summary>是否关键字</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static Boolean IsKeyWord(String name)
        {
            if (String.IsNullOrEmpty(name)) return false;

            // 特殊处理item
            if (String.Equals(name, "item", StringComparison.OrdinalIgnoreCase)) return true;

            // 只要有大写字母，就不是关键字
            if (name.Any(c => c >= 'A' && c <= 'Z')) return false;

            return !_CG.IsValidIdentifier(name);
        }

        /// <summary>获取显示名，如果描述不存在，则使用名称，否则使用描述前面部分，句号（中英文皆可）、换行分隔</summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public virtual String GetDisplayName(String name, String description)
        {
            if (String.IsNullOrEmpty(description)) return name;

            String str = description.Trim();
            Int32 p = str.IndexOfAny(new Char[] { '.', '。', '\r', '\n' });
            // p=0表示符号在第一位，不考虑
            if (p > 0) str = str.Substring(0, p).Trim();

            return str;
        }
        #endregion

        #region 模型处理
        /// <summary>连接两个表。
        /// 实际上是猜测它们之间的关系，根据一个字段名是否等于另一个表的表名加某个字段名来判断是否存在关系。</summary>
        /// <param name="table"></param>
        /// <param name="rtable"></param>
        public virtual IDataTable Connect(IDataTable table, IDataTable rtable)
        {
            foreach (var dc in table.Columns)
            {
                if (dc.PrimaryKey || dc.Identity) continue;

                if (GuessRelation(table, rtable, rtable.Name, dc, dc.Name)) continue;
                if (!dc.Name.EqualIgnoreCase(dc.Alias))
                {
                    if (GuessRelation(table, rtable, rtable.Name, dc, dc.Alias)) continue;
                }

                //if (String.Equals(rtable.Alias, rtable.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (rtable.Name.EqualIgnoreCase(rtable.Alias)) continue;

                // 如果表2的别名和名称不同，还要继续
                if (GuessRelation(table, rtable, rtable.Alias, dc, dc.Name)) continue;
                if (!dc.Name.EqualIgnoreCase(dc.Alias))
                {
                    if (GuessRelation(table, rtable, rtable.Alias, dc, dc.Alias)) continue;
                }
            }

            return table;
        }

        /// <summary>猜测表间关系</summary>
        /// <param name="table"></param>
        /// <param name="rtable"></param>
        /// <param name="rname"></param>
        /// <param name="column"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual Boolean GuessRelation(IDataTable table, IDataTable rtable, String rname, IDataColumn column, String name)
        {
            if (name.Length <= rtable.Name.Length || !name.StartsWith(rtable.Name, StringComparison.OrdinalIgnoreCase)) return false;

            var key = name.Substring(rtable.Name.Length);
            var dc = rtable.GetColumn(key);
            // 猜测两表关联关系时，两个字段的类型也必须一致
            if (dc == null || dc.DataType != column.DataType) return false;

            // 建立关系
            var dr = table.CreateRelation();
            dr.Column = column.Name;
            dr.RelationTable = rtable.Name;
            dr.RelationColumn = dc.Name;
            // 表关系这里一般是多对一，比如管理员的RoleID=>Role+Role.ID，对于索引来说，不是唯一的
            dr.Unique = false;
            // 当然，如果这个字段column有唯一索引，那么，这里也是唯一的。这就是典型的一对一
            if (column.PrimaryKey || column.Identity)
                dr.Unique = true;
            else
            {
                var di = table.GetIndex(column.Name);
                if (di != null && di.Unique) dr.Unique = true;
            }

            dr.Computed = true;
            table.Relations.Add(dr);

            // 给另一方建立关系
            //foreach (IDataRelation item in rtable.Relations)
            //{
            //    if (item.Column == dc.Name && item.RelationTable == table.Name && item.RelationColumn == column.Name) return dr;
            //}
            if (rtable.GetRelation(dc.Name, table.Name, column.Name) != null) return true;

            dr = rtable.CreateRelation();
            dr.Column = dc.Name;
            dr.RelationTable = table.Name;
            dr.RelationColumn = column.Name;
            // 那么这里就是唯一的啦
            dr.Unique = true;
            // 当然，如果字段dc不是主键，也没有唯一索引，那么关系就不是唯一的。这就是典型的多对多
            if (!dc.PrimaryKey && !dc.Identity)
            {
                var di = rtable.GetIndex(dc.Name);
                // 没有索引，或者索引不是唯一的
                if (di == null || !di.Unique) dr.Unique = false;
            }

            dr.Computed = true;
            rtable.Relations.Add(dr);

            return true;
        }

        /// <summary>修正数据</summary>
        /// <param name="table"></param>
        public virtual IDataTable Fix(IDataTable table)
        {
            #region 根据单字段索引修正对应的关系
            // 给所有单字段索引建立关系，特别是一对一关系
            foreach (IDataIndex item in table.Indexes)
            {
                if (item.Columns == null || item.Columns.Length != 1) continue;

                IDataRelation dr = table.GetRelation(item.Columns[0]);
                if (dr == null) continue;

                dr.Unique = item.Unique;
                // 跟关系有关联的索引
                dr.Computed = item.Computed;
            }
            #endregion

            #region 给所有关系字段建立索引
            foreach (IDataRelation dr in table.Relations)
            {
                // 跳过主键
                IDataColumn dc = table.GetColumn(dr.Column);
                if (dc == null || dc.PrimaryKey) continue;

                if (table.GetIndex(dr.Column) == null)
                {
                    IDataIndex di = table.CreateIndex();
                    di.Columns = new String[] { dr.Column };
                    // 这两个的关系，唯一性
                    di.Unique = dr.Unique;
                    di.Computed = true;
                    table.Indexes.Add(di);
                }
            }
            #endregion

            #region 从索引中修正主键
            IDataColumn[] pks = table.PrimaryKeys;
            if (pks == null || pks.Length < 1)
            {
                // 在索引中找唯一索引作为主键
                foreach (IDataIndex item in table.Indexes)
                {
                    if (!item.PrimaryKey || item.Columns == null || item.Columns.Length < 1) continue;

                    pks = table.GetColumns(item.Columns);
                    if (pks != null && pks.Length > 0) Array.ForEach<IDataColumn>(pks, dc => dc.PrimaryKey = true);
                }
            }
            pks = table.PrimaryKeys;
            if (pks == null || pks.Length < 1)
            {
                // 在索引中找唯一索引作为主键
                foreach (IDataIndex item in table.Indexes)
                {
                    if (!item.Unique || item.Columns == null || item.Columns.Length < 1) continue;

                    pks = table.GetColumns(item.Columns);
                    if (pks != null && pks.Length > 0) Array.ForEach<IDataColumn>(pks, dc => dc.PrimaryKey = true);
                }
            }
            pks = table.PrimaryKeys;
            if (pks == null || pks.Length < 1)
            {
                // 如果还没有主键，把第一个索引作为主键
                foreach (IDataIndex item in table.Indexes)
                {
                    if (item.Columns == null || item.Columns.Length < 1) continue;

                    pks = table.GetColumns(item.Columns);
                    if (pks != null && pks.Length > 0) Array.ForEach<IDataColumn>(pks, dc => dc.PrimaryKey = true);
                }
            }
            #endregion

            #region 最后修复主键
            if (table.PrimaryKeys.Length < 1)
            {
                // 自增作为主键，然后是ID/Guid/UID，最后默认使用第一个
                // 没办法，如果没有主键，整个实体层都会面临大问题！
                IDataColumn dc = null;
                if ((dc = table.Columns.FirstOrDefault(c => c.Identity)) != null)
                    dc.PrimaryKey = true;
                //else if ((dc = table.Columns.FirstOrDefault(c => c.Is("ID"))) != null)
                //    dc.PrimaryKey = true;
                //else if ((dc = table.Columns.FirstOrDefault(c => c.Is("Guid"))) != null)
                //    dc.PrimaryKey = true;
                //else if ((dc = table.Columns.FirstOrDefault(c => c.Is("UID"))) != null)
                //    dc.PrimaryKey = true;
                //else if ((dc = table.Columns.FirstOrDefault()) != null)
                //    dc.PrimaryKey = true;
            }
            #endregion

            #region 给非主键的自增字段建立唯一索引
            foreach (var dc in table.Columns)
            {
                if (dc.Identity && !dc.PrimaryKey)
                {
                    var di = table.GetIndex(dc.Name);
                    if (di == null)
                    {
                        di = table.CreateIndex();
                        di.Columns = new String[] { dc.Name };
                        di.Computed = true;
                    }
                    // 不管是不是原来有的索引，都要唯一
                    di.Unique = true;
                }
            }
            #endregion

            #region 索引应该具有跟字段一样的唯一和主键约束
            // 主要针对MSSQL2000
            foreach (var di in table.Indexes)
            {
                if (di.Columns == null) continue;

                var dcs = table.GetColumns(di.Columns);
                if (dcs == null || dcs.Length <= 0) continue;

                if (!di.Unique) di.Unique = dcs.All(dc => dc.Identity);
                if (!di.PrimaryKey) di.PrimaryKey = dcs.All(dc => dc.PrimaryKey);
            }
            #endregion

            #region 修正可能错误的别名
            var ns = new List<String>();
            ns.Add(table.Alias);
            foreach (var item in table.Columns)
            {
                if (ns.Contains(item.Alias) || IsKeyWord(item.Alias))
                {
                    // 通过加数字的方式，解决关键字问题
                    for (int i = 2; i < table.Columns.Count; i++)
                    {
                        String name = item.Alias + i;
                        // 加了数字后，不可能是关键字
                        if (!ns.Contains(name))
                        {
                            item.Alias = name;
                            break;
                        }
                    }
                }

                ns.Add(item.Alias);
            }
            #endregion

            return table;
        }

        /// <summary>修正数据列</summary>
        /// <param name="column"></param>
        public virtual IDataColumn Fix(IDataColumn column)
        {
            return column;
        }
        #endregion

        #region 静态实例
        /// <summary>当前名称解析器</summary>
        public static IModelResolver Current { get { return XCodeService.ResolveInstance<IModelResolver>(); } }
        #endregion
    }
}