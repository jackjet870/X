﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Web;
using System.Xml.Serialization;
using NewLife.CommonEntity.Exceptions;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Security;
using NewLife.Web;
using XCode;
using System.Linq;

namespace NewLife.CommonEntity
{
    /// <summary>管理员</summary>
    /// <typeparam name="TEntity">管理员实体类</typeparam>
    /// <typeparam name="TRoleEntity">角色实体类</typeparam>
    /// <typeparam name="TMenuEntity">菜单实体类</typeparam>
    /// <typeparam name="TRoleMenuEntity">角色菜单实体类</typeparam>
    /// <typeparam name="TLogEntity">日志实体类</typeparam>
    [Serializable]
    public partial class Administrator<TEntity, TRoleEntity, TMenuEntity> : Administrator<TEntity>
        where TEntity : Administrator<TEntity, TRoleEntity, TMenuEntity>, new()
        where TRoleEntity : Role<TRoleEntity>, new()
        where TMenuEntity : Menu<TMenuEntity>, new()
    {
        #region 对象操作
        //static Administrator()
        //{
        //    // 初始化时执行必要的权限检查，以防万一管理员无法操作
        //}

        /// <summary>初始化数据</summary>
        protected override void InitData()
        {
            base.InitData();

            CheckRole();
        }

        /// <summary>初始化时执行必要的权限检查，以防万一管理员无法操作</summary>
        static void CheckRole()
        {
            var rs = Role<TRoleEntity>.Meta.Cache.Entities;
            if (rs.Count <= 0) return;
            var list = rs.ToList();

            // 如果某些菜单已经被删除，但是角色权限表仍然存在，则删除
            var ids = Menu<TMenuEntity>.FindAllWithCache().GetItem<Int32>("ID").ToArray();
            foreach (var role in rs)
            {
                if (!role.CheckValid(ids))
                {
                    XTrace.WriteLine("删除[{0}]中的无效资源权限！", role);
                    role.Save();
                }
            }

            var sys = list.FirstOrDefault(e => e.IsSystem);
            if (sys == null) return;

            // 如果没有任何角色拥有权限管理的权限，那是很悲催的事情
            var count = 0;
            var nes = Menu<TMenuEntity>.Necessaries;
            foreach (var item in nes)
            {
                if (!list.Any(e => e.Has(item, PermissionFlags.All)))
                {
                    count++;
                    sys.Set(item, PermissionFlags.All);
                }
            }
            if (count > 0)
            {
                XTrace.WriteLine("共有{0}个必要菜单，没有任何角色拥有权限，准备授权第一系统角色[{1}]拥有其完全管理权！", count, sys);
                sys.Save();
            }
        }

        /// <summary>已重载。调用Save时写日志，而调用Insert和Update时不写日志</summary>
        /// <returns></returns>
        public override int Save()
        {
            if (ID == 0)
                WriteLog(null, "添加", Name);
            else
                WriteLog(null, "修改", Name);

            return base.Save();
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override int Delete()
        {
            String name = Name;
            if (String.IsNullOrEmpty(name))
            {
                var entity = Find("ID", ID);
                if (entity != null) name = entity.Name;
            }
            WriteLog(null, "删除", name);

            return base.Delete();
        }
        #endregion

        #region 权限日志
        /// <summary>角色</summary>
        /// <remarks>扩展属性不缓存空对象，一般来说，每个管理员都有对应的角色，如果没有，可能是在初始化</remarks>
        [XmlIgnore]
        public virtual TRoleEntity Role
        {
            get
            {
                if (RoleID <= 0) return null;
                //var role = Extends.GetExtend<TRoleEntity, TRoleEntity>("Role", e => Role<TRoleEntity, TMenuEntity, TRoleMenuEntity>.FindByID(RoleID), false);
                //// 如果找不到角色，并且处于初始化状态，则更正数据
                //if (role == null && Meta.Count <= 1 && Role<TRoleEntity>.Meta.Count > 0)
                //{
                //    role = Role<TRoleEntity>.Meta.Cache.Entities[0];
                //    RoleID = role.ID;
                //    this.Save();
                //}
                //return role;
                return Role<TRoleEntity>.FindByID(RoleID);
            }
            //set { Extends.SetExtend<TRoleEntity>("Role", value); }
        }

        /// <summary>角色</summary>
        internal protected override IRole RoleInternal { get { return Role; } /*set { Role = (TRoleEntity)value; }*/ }

        /// <summary>根据权限名（权限路径）找到权限菜单实体</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public override IMenu FindPermissionMenu(string name)
        {
            // 优先使用当前页，除非当前页与权限名不同
            var entity = Menu<TMenuEntity>.Current;
            if (entity != null && entity.Permission == name) return entity;

            // 根据权限名找
            var menu = Menu<TMenuEntity>.FindForPerssion(name);
            if (menu != null) return menu;

            //取消菜单名称检查
            //// 找不到的时候，修改当前页面
            //if (menu == null)
            //{
            //    if (entity != null)
            //    {
            //        if (entity.ResetName(name)) menu = entity;
            //    }
            //}

            return menu;
        }

        ///// <summary>创建当前管理员的日志实体</summary>
        ///// <param name="type">类型</param>
        ///// <param name="action"></param>
        ///// <returns></returns>
        //public override ILog CreateLog(Type type, string action)
        //{
        //    var log = Log<TLogEntity>.Create(type, action);
        //    log.UserID = ID;
        //    log.UserName = FriendName;

        //    return log;
        //}
        #endregion
    }

    /// <summary>管理员</summary>
    /// <remarks>
    /// 基础实体类应该是只有一个泛型参数的，需要用到别的类型时，可以继承一个，也可以通过虚拟重载等手段让基类实现
    /// </remarks>
    /// <typeparam name="TEntity">管理员类型</typeparam>
    public abstract partial class Administrator<TEntity> : CommonEntityBase<TEntity>, IAdministrator, IManageUser//, IPrincipal//, IIdentity
        where TEntity : Administrator<TEntity>, new()
    {
        #region 对象操作
        static Administrator()
        {
            // 用于引发基类的静态构造函数
            var entity = new TEntity();

            //!!! 曾经这里导致产生死锁
            // 这里是静态构造函数，访问Factory引发EntityFactory.CreateOperate，
            // 里面的EnsureInit会等待实体类实例化完成，实体类的静态构造函数还卡在这里
            // 不过这不是理由，同一个线程遇到同一个锁不会堵塞
            // 发生死锁的可能性是这里引发EnsureInit，而另一个线程提前引发EnsureInit拿到锁
            Meta.Factory.AdditionalFields.Add(__.Logins);
        }

        /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override void InitData()
        {
            base.InitData();

            if (Meta.Count > 0) return;

            if (XTrace.Debug) XTrace.WriteLine("开始初始化{0}管理员数据……", typeof(TEntity).Name);

            var user = new TEntity();
            user.Name = "admin";
            user.Password = DataHelper.Hash("admin");
            user.DisplayName = "管理员";
            user.RoleID = 1;
            user.IsEnable = true;
            user.Insert();

            if (XTrace.Debug) XTrace.WriteLine("完成初始化{0}管理员数据！", typeof(TEntity).Name);
        }

        /// <summary>验证</summary>
        /// <param name="isNew"></param>
        public override void Valid(bool isNew)
        {
            base.Valid(isNew);

            if (String.IsNullOrEmpty(Name)) throw new ArgumentNullException(__.Name, "用户名不能为空！");
            if (RoleID < 1) throw new ArgumentNullException(__.RoleID, "没有指定角色！");
        }
        #endregion

        #region 扩展属性
        static HttpState<TEntity> _httpState;
        /// <summary>Http状态，子类可以重新给HttpState赋值，以控制保存Http状态的过程</summary>
        public static HttpState<TEntity> HttpState
        {
            get
            {
                if (_httpState != null) return _httpState;
                _httpState = new HttpState<TEntity>("Admin");
                _httpState.CookieToEntity = new Converter<HttpCookie, TEntity>(delegate(HttpCookie cookie)
                {
                    if (cookie == null) return null;

                    var user = HttpUtility.UrlDecode(cookie["u"]);
                    var pass = cookie["p"];
                    if (String.IsNullOrEmpty(user) || String.IsNullOrEmpty(pass)) return null;

                    try
                    {
                        return Login(user, pass, -1);
                    }
                    catch (DbException ex)
                    {
                        XTrace.WriteLine("{0}登录失败！{1}", user, ex);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        WriteLog("登录", user + "登录失败！" + ex.Message);
                        return null;
                    }
                });
                _httpState.EntityToCookie = new Converter<TEntity, HttpCookie>(delegate(TEntity entity)
                {
                    var cookie = HttpContext.Current.Response.Cookies[_httpState.Key];
                    if (entity != null)
                    {
                        cookie["u"] = HttpUtility.UrlEncode(entity.Name);
                        cookie["p"] = !String.IsNullOrEmpty(entity.Password) ? DataHelper.Hash(entity.Password) : null;
                    }
                    else
                    {
                        cookie.Value = null;
                    }

                    return cookie;
                });

                return _httpState;
            }
            set { _httpState = value; }
        }

        /// <summary>当前登录用户</summary>
        public static TEntity Current
        {
            get
            {
                var entity = HttpState.Current;
                if (HttpState.Get(null, null) != entity) HttpState.Current = entity;
                return entity;
            }
            set
            {
                HttpState.Current = value;
                //Thread.CurrentPrincipal = (IPrincipal)value;
            }
        }

        /// <summary>当前登录用户，不带自动登录</summary>
        public static TEntity CurrentNoAutoLogin { get { return HttpState.Get(null, null); } }

        /// <summary>当前登录用户。通过实体资格提供者，保证取得正确的管理员</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("该成员在后续版本中讲不再被支持！")]
        public static IAdministrator CurrentAdministrator
        {
            get
            {
                //return TypeResolver.GetPropertyValue(typeof(IAdministrator), "Current") as IAdministrator;
                return ManageProvider.Provider.Current as IAdministrator;
            }
        }

        /// <summary>友好名字</summary>
        public virtual String FriendName { get { return String.IsNullOrEmpty(DisplayName) ? Name : DisplayName; } }
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TEntity FindByID(Int32 id)
        {
            if (Meta.Count >= 1000)
                return Find(__.ID, id);
            else // 实体缓存
                return Meta.Cache.Entities.Find(__.ID, id);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public static TEntity FindByName(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;

            if (Meta.Count >= 1000)
                return Find(__.Name, name);
            else // 实体缓存
                return Meta.Cache.Entities.Find(__.Name, name);
        }

        /// <summary>根据邮箱地址查找</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TEntity FindByMail(String mail)
        {
            if (Meta.Count >= 1000)
                return Find(__.Mail, mail);
            else // 实体缓存
                return Meta.Cache.Entities.Find(__.Mail, mail);
        }

        /// <summary>根据手机号码查找</summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        public static TEntity FindByPhone(String phone)
        {
            if (Meta.Count >= 1000)
                return Find(__.Phone, phone);
            else // 实体缓存
                return Meta.Cache.Entities.Find(__.Phone, phone);
        }

        /// <summary>根据唯一代码查找</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TEntity FindByCode(String code)
        {
            if (Meta.Count >= 1000)
                return Find(__.Code, code);
            else // 实体缓存
                return Meta.Cache.Entities.Find(__.Code, code);
        }

        ///// <summary>根据SSOUserID查找所有帐户</summary>
        ///// <param name="id"></param>
        ///// <returns></returns>
        //public static EntityList<TEntity> FindAllBySSOUserID(Int32 id)
        //{
        //    if (Meta.Count >= 1000)
        //        return FindAll(__.SSOUserID, id);
        //    else // 实体缓存
        //        return Meta.Cache.Entities.FindAll(__.SSOUserID, id);
        //}

        /// <summary>查询满足条件的记录集，分页、排序</summary>
        /// <param name="key">关键字</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="orderClause">排序，不带Order By</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>实体集</returns>
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public static EntityList<TEntity> Search(String key, Int32 roleId, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindAll(SearchWhere(key, roleId), orderClause, null, startRowIndex, maximumRows);
        }

        /// <summary>查询满足条件的记录总数，分页和排序无效，带参数是因为ObjectDataSource要求它跟Search统一</summary>
        /// <param name="key">关键字</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="orderClause">排序，不带Order By</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>记录数</returns>
        public static Int32 SearchCount(String key, Int32 roleId, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindCount(SearchWhere(key, roleId), null, null, 0, 0);
        }

        /// <summary>查询满足条件的记录集，分页、排序</summary>
        /// <param name="key">关键字</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="isEnable">是否启用</param>
        /// <param name="orderClause">排序，不带Order By</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>实体集</returns>
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public static EntityList<TEntity> Search(String key, Int32 roleId, Boolean? isEnable, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindAll(SearchWhere(key, roleId, isEnable), orderClause, null, startRowIndex, maximumRows);
        }

        /// <summary>查询满足条件的记录总数，分页和排序无效，带参数是因为ObjectDataSource要求它跟Search统一</summary>
        /// <param name="key">关键字</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="isEnable">是否启用</param>
        /// <param name="orderClause">排序，不带Order By</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>记录数</returns>
        public static Int32 SearchCount(String key, Int32 roleId, Boolean? isEnable, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindCount(SearchWhere(key, roleId, isEnable), null, null, 0, 0);
        }

        /// <summary>构造搜索条件</summary>
        /// <param name="key">关键字</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="isEnable">是否启用</param>
        /// <returns></returns>
        private static String SearchWhere(String key, Int32 roleId, Boolean? isEnable = null)
        {
            var exp = new WhereExpression();

            // SearchWhereByKeys系列方法用于构建针对字符串字段的模糊搜索
            if (!String.IsNullOrEmpty(key)) exp &= SearchWhereByKey(key);

            if (roleId > 0) exp &= _.RoleID == roleId;
            if (isEnable != null) exp &= _.IsEnable == isEnable.Value;

            return exp;
        }
        #endregion

        #region 扩展操作
        /// <summary>已重载。显示友好名字</summary>
        /// <returns></returns>
        public override string ToString() { return FriendName; }
        #endregion

        #region 业务
        /// <summary>登录</summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static TEntity Login(String username, String password)
        {
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            //if (String.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

            try
            {
                return Login(username, password, 1);
            }
            catch (Exception ex)
            {
                WriteLog("登录", username + "登录失败！" + ex.Message);
                throw;
            }
        }

        static TEntity Login(String username, String password, Int32 hashTimes)
        {
            //if (String.IsNullOrEmpty(username)) return null;
            //if (String.IsNullOrEmpty(username)) throw new Exception("该帐号不存在！");
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username", "该帐号不存在！");
            //过滤帐号中的空格，防止出现无操作无法登录的情况
            var account = username.Trim();
            var user = FindByName(account);
            //if (user == null) return null;
            if (user == null) throw new EntityException("帐号{0}不存在！", account);

            if (!user.IsEnable) throw new EntityException("账号{0}被禁用！", account);

            // 数据库为空密码，任何密码均可登录
            if (!String.IsNullOrEmpty(user.Password))
            {
                if (hashTimes > 0)
                {
                    var p = password;
                    if (!String.IsNullOrEmpty(p))
                    {
                        for (int i = 0; i < hashTimes; i++)
                        {
                            p = DataHelper.Hash(p);
                        }
                    }
                    if (!p.EqualIgnoreCase(user.Password)) throw new EntityException("密码不正确！");
                }
                else
                {
                    var p = user.Password;
                    for (int i = 0; i > hashTimes; i--)
                    {
                        p = DataHelper.Hash(p);
                    }
                    if (!p.EqualIgnoreCase(password)) throw new EntityException("密码不正确！");
                }
            }

            user.SaveLoginInfo();

            Current = user;

            if (hashTimes == -1)
                WriteLog("自动登录", username);
            else
                WriteLog("登录", username);

            return user;
        }

        /// <summary>保存登录信息</summary>
        /// <returns></returns>
        protected virtual Int32 SaveLoginInfo()
        {
            Logins++;
            LastLogin = DateTime.Now;
            var ip = WebHelper.UserHost;
            if (!String.IsNullOrEmpty(ip)) LastLoginIP = ip;
            return Update();
        }

        /// <summary>注销</summary>
        public virtual void Logout()
        {
            WriteLog("注销", Name);
            Current = null;
            //Thread.CurrentPrincipal = null;
        }

        /// <summary>根据权限名（权限路径）找到权限菜单实体</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public abstract IMenu FindPermissionMenu(String name);

        /// <summary>拥有指定菜单的权限</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public virtual Boolean HasMenu(String name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            var menu = FindPermissionMenu(name);
            if (menu == null) return false;

            //return Acquire((Int32)menu["ID"], PermissionFlags.None);
            return Acquire(menu.ID, PermissionFlags.None);
        }

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="name">名称</param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public virtual Boolean Acquire(String name, PermissionFlags flag)
        {
            var menu = FindPermissionMenu(name);
            if (menu == null) return false;

            return Acquire(menu.ID, flag);
        }

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public virtual Boolean Acquire(String name)
        {
            return Acquire(name, PermissionFlags.None);
        }

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="menuID"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public virtual Boolean Acquire(Int32 menuID, PermissionFlags flag)
        {
            if (menuID <= 0) throw new ArgumentNullException("menuID");

            var entity = (this as IAdministrator).Role;
            if (entity == null) return false;

            // 申请权限
            //return entity.Acquire(menuID, flag);
            return true;
        }

        ///// <summary>创建当前管理员的日志实体</summary>
        ///// <param name="type">类型</param>
        ///// <param name="action"></param>
        ///// <returns></returns>
        //public abstract ILog CreateLog(Type type, String action);

        ///// <summary>写日志</summary>
        ///// <param name="action">操作</param>
        ///// <param name="remark">备注</param>
        //public static void WriteLog(String action, String remark)
        //{
        //    //IEntityOperate op = EntityFactory.CreateOperate(TypeResolver.Resolve(typeof(IAdministrator), null));

        //    var provider = CommonManageProvider.Provider;
        //    if (provider == null) return;

        //    var op = EntityFactory.CreateOperate(provider.AdminstratorType);
        //    var admin = op.Default as IAdministrator;
        //    if (admin != null) admin.WriteLog(typeof(TEntity), action, remark);
        //}
        #endregion

        #region IAdministrator 成员
        /// <summary>角色</summary>
        [XmlIgnore]
        IRole IAdministrator.Role { get { return RoleInternal; } /*set { RoleInternal = value; }*/ }

        /// <summary>角色</summary>
        [XmlIgnore]
        internal protected abstract IRole RoleInternal { get; /*set;*/ }

        /// <summary>角色名</summary>
        public virtual String RoleName { get { return RoleInternal == null ? null : RoleInternal.Name; } set { } }

        /// <summary>写日志</summary>
        /// <param name="type">类型</param>
        /// <param name="action">操作</param>
        /// <param name="remark">备注</param>
        public void WriteLog(Type type, String action, String remark)
        {
            if (!Config.GetConfig<Boolean>("NewLife.CommonEntity.WriteEntityLog", true)) return;

            //if (type == null) type = this.GetType();
            //var log = CreateLog(type, action);
            //if (log != null)
            //{
            //    log.Remark = remark;
            //    log.Save();
            //}
        }
        #endregion

        #region IPrincipal 成员
        //[NonSerialized]
        //private IIdentity idt;
        //[XmlIgnore]
        //IIdentity IPrincipal.Identity
        //{
        //    get { return idt ?? (idt = new GenericIdentity(Name, "CommonEntity")); }
        //}

        //bool IPrincipal.IsInRole(string role)
        //{
        //    return RoleName == role;
        //}
        #endregion

        #region IIdentity 成员
        //[XmlIgnore]
        //string IIdentity.AuthenticationType
        //{
        //    get { return "CommonEntity"; }
        //}

        //[XmlIgnore]
        //bool IIdentity.IsAuthenticated
        //{
        //    get { return true; }
        //}

        //string IIdentity.Name
        //{
        //    get { return Name; }
        //}
        #endregion

        #region IManageUser 成员
        /// <summary>编号</summary>
        object IManageUser.Uid { get { return ID; } }

        /// <summary>账号</summary>
        string IManageUser.Account { get { return Name; } set { Name = value; } }

        /// <summary>密码</summary>
        string IManageUser.Password { get { return Password; } set { Password = value; } }

        /// <summary>是否管理员</summary>
        Boolean IManageUser.IsAdmin { get { return RoleName == "管理员" || RoleName == "超级管理员"; } set { } }

        [NonSerialized]
        IDictionary<String, Object> _Properties;
        /// <summary>属性集合</summary>
        IDictionary<String, Object> IManageUser.Properties
        {
            get
            {
                if (_Properties == null)
                {
                    _Properties = new Dictionary<String, Object>();
                    foreach (var item in Meta.FieldNames)
                    {
                        _Properties[item] = this[item];
                    }
                    foreach (var item in Extends)
                    {
                        _Properties[item.Key] = item.Value;
                    }
                }
                return _Properties;
            }
        }
        #endregion
    }

    public partial interface IAdministrator
    {
        /// <summary>友好名字</summary>
        String FriendName { get; }

        /// <summary>角色</summary>
        IRole Role { get; /*set;*/ }

        /// <summary>角色名</summary>
        String RoleName { get; set; }

        /// <summary>根据权限名（权限路径）找到权限菜单实体</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        IMenu FindPermissionMenu(String name);

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="menuID"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        Boolean Acquire(Int32 menuID, PermissionFlags flag);

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="name">名称</param>
        /// <param name="flag"></param>
        /// <returns></returns>
        Boolean Acquire(String name, PermissionFlags flag);

        /// <summary>申请指定菜单指定操作的权限</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        Boolean Acquire(String name);

        ///// <summary>创建指定类型指定动作的日志实体</summary>
        ///// <param name="type">类型</param>
        ///// <param name="action"></param>
        ///// <returns></returns>
        //ILog CreateLog(Type type, String action);

        ///// <summary>写日志</summary>
        ///// <param name="type">类型</param>
        ///// <param name="action">操作</param>
        ///// <param name="remark">备注</param>
        //void WriteLog(Type type, String action, String remark);

        /// <summary>注销</summary>
        void Logout();

        /// <summary>保存</summary>
        /// <returns></returns>
        Int32 Save();
    }
}