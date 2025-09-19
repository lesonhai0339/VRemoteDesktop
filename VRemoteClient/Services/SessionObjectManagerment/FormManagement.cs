using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Utils;

namespace VRemoteClient.Services.SessionManagerment
{
    public class FormManagement<T> where T : Form, IDisposable, new()
    {
        private bool _isDisposed;
        private ConcurrentDictionary<string, T> _listObject;
        public FormManagement()
        {
            _isDisposed = false;    
            _listObject = new ConcurrentDictionary<string, T>();
        }

        #region Properties
        public ConcurrentDictionary<string, T> ListObject
        {
            get => _listObject;
        }
        #endregion
        #region Methods
        public T? GetFormById(string id)
        {
            try
            {
                return _listObject.TryGetValue(id, out T value) ? value : null;
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when get form by id");
                return null;
            }
        }
        public T GetOrAdd(string id, Func<string, T> factory)
        {
            try
            {
                return _listObject.GetOrAdd(id, factory);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when create or add");
                return null;
            }
        }
        public bool AddForm(string id)
        {
            try
            {
                T form = new T();
                return _listObject.TryAdd(id, form);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when add new form");
                return false;
            }
        }
        public bool AddForm(string id, T form)
        {
            try
            {
               return _listObject.TryAdd(id, form);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when add new form");
                return false;
            }
        }
        public bool UpdateForm(string id, T form)
        {
            try
            {
                if(_listObject.TryGetValue(id, out var obj))
                {
                    return _listObject.TryUpdate(id, form, obj);
                }
                return false;
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when add new form");
                return false;
            }
        }
        public bool RemoveForm(string id)
        {
            try
            {
                return _listObject.TryRemove(id, out _);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when remove form");
                return false;
            }
        }
        public void ShowForm(string id)
        {
            try
            {
               if(_listObject.TryGetValue(id, out var form))
                {
                    if (form.IsDisposed)
                    {
                        RemoveForm(id);
                        Log.ForContext("FileName", "FormManagerment").Error(string.Format("Remove form with id: {0} because it have been disposed", id));
                        return;
                    }

                    if (form.InvokeRequired)
                    {
                        var _ = form.Handle;
                        form.BeginInvoke((MethodInvoker)(() =>
                        {
                            if (!form.IsDisposed)
                            {
                                SafelyShowForm(form);
                            }
                        }));
                    }
                    else
                    {
                        SafelyShowForm(form);
                    }
                }
            }
            catch(ObjectDisposedException ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when show form");
            }
            catch (InvalidOperationException ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when show form");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when show form");
            }
        }
        private void SafelyShowForm(T form)
        {
            if (form.IsDisposed)
                return;

            form.Show();
            form.BringToFront();
            
            if(form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            try
            {
                form.Activate();
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Cannot active form");
            }
        }
        //public DialogResult ShowDialogForm(string id)
        //{
        //    try
        //    {
        //        if (_listObject.TryGetValue(id, out var form))
        //        {
        //            var result = form.ShowDialog();
        //            return result;
        //        }
        //        return DialogResult.None;
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when show dialog form");
        //        return DialogResult.None;
        //    }
        //}
        public void CloseForm(string id)
        {
            try
            {
                if (_listObject.TryGetValue(id, out var form))
                {
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Error when show form");
            }
        }
        private void Clear()
        {
            try
            {
                foreach(var form in _listObject.Values)
                {
                    form.Dispose();
                }
                _listObject.Clear();
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormManagerment").Error(ex, "Cannot clear");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (!_isDisposed)
            {
                Clear();
            }
            _isDisposed = true;
        }
        #endregion
    }
}
