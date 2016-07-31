
var StoreView = function () {

    var that = this;

    //    this.init = function () {
    //        this.loadUsers();
    //        $('#categories').multiselect({
    //            //includeSelectAllOption: true,
    //            //buttonClass: "col-sm-12"
    //        });
    //        $('#btn-add').on('click', that.showForm);
    //        $('#btn-submit').on('click', that.submitForm);
    //        $('#dlg-add').on('shown.bs.modal', function () {
    //            $('#email').focus();
    //        });
    //    };

    //    this.loadUsers = function () {

    //        $('#pnl-loading').show();

    //        var request = $.ajax({
    //            url: "/user/list",
    //            method: "GET",
    //            dataType: 'html'
    //        });

    //        request.done(function (response) {
    //            //debugger;
    //            $('#pnl-users').html(response);
    //        });

    //        request.fail(function (xhr, textStatus, errorThrown) {
    //            alert('error: ' + xhr.responseText);
    //        });
    //        request.always(function (xhr, textStatus) {
    //            //debugger;
    //            $('#pnl-loading').hide();
    //        });
    //    };

    //    this.showError = function (error) {
    //        //debugger;
    //        var err = error;
    //        if ($.isArray(err)) {
    //            err = Collections.displayList(err);
    //        }
    //        $("#msg-error").html(err);
    //        $("#msg-error").removeClass('hidden');
    //    };

    //    this.showForm = function () {
    //        $("#msg-error").addClass('hidden');
    //        $('#dlg-add').modal('show');
    //    };

    //    this.submitForm = function () {
    //        $("#msg-error").addClass('hidden');
    ////        debugger;
    //        var formData = $('#form-user').serializeForm();
    //        var request = $.ajax({
    //            url: "/user",
    //            method: "POST",
    //            data: formData,
    //            dataType: 'json',
    //            traditional: true
    //        });

    //        request.done(function (response) {
    //            //debugger;
    //            if (response.success) {
    //                $('#dlg-add').modal('hide');
    //                that.loadUsers();
    //            }
    //            else {
    //                that.showError(response.messages);
    //            }
    //        });

    //        request.fail(function (xhr, textStatus) {
    //            try {
    //                that.showError(xhr.responseJSON.message);
    //            }
    //            catch (err) {
    //                that.showError('A fatal error occurred');
    //            }
    //        });
    //    };

};

$(document).ready(function()
{
    new StoreView().init();
});
