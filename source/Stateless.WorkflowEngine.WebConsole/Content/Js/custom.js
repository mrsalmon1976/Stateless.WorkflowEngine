﻿$.fn.serializeForm = function () {
    var o = {};
    var a = this.serializeArray();
    $.each(a, function () {
        if (o[this.name] !== undefined) {
            if (!o[this.name].push) {
                o[this.name] = [o[this.name]];
            }
            o[this.name].push(this.value || '');
        } else {
            o[this.name] = this.value || '';
        }
    });
    return o;
};

var Collections = function () { };

// Get a random integer between 'min' and 'max'.
Collections.displayList = function (arr) {
    var html = '<ul>';
    for (var i = 0; i < arr.length; i++) {
        html += '<li>' + arr[i] + '</li>';
    }
    html += '</ul>';
    return html;
}

var Numeric = function () { };

// Get a random integer between 'min' and 'max'.
Numeric.getRandomInt = function(min, max) {
    return Math.floor(Math.random() * (max - min + 1) + min);
}

var Utils = function () { };

// handles an ajax error by redirecting to the login screen if there was an authorisation error, 
// or displaying the error in an element specifed as a jquery object
Utils.handleAjaxError = function (xhr, jqMessagePanel) {
    if (xhr.status == 401) {
        bootbox.alert('You do not have authorisation to perform this action; you will now be redirected to the login page.', function (result) {
            window.location.href = '/login';
        });
        return;
    }
    var msg = xhr.statusText;
    try
    {
        var json = JSON.parse(msg);
        msg = json.message;
    }
    catch (error)
    { 
    }
    if (msg == null || msg.length == 0) {
        msg = 'An unspecified error occurred.';
    }
    jqMessagePanel.html('<div class="alert alert-danger" role="alert">' + msg + '</div>');
};

Utils.showError = function (selector, error) {
    //debugger;
    var err = error;
    if ($.isArray(err)) {
        err = Collections.displayList(err);
    }
    $(selector).html(err);
    $(selector).removeClass('hidden');
};

var MainLayout = function () {

    var that = this;

    this.init = function () {
        $('#mnu-change-password').on('click', that.showProfileModal);
        $('#profile-btn-submit').on('click', that.submitChangePassword);
    };

    this.showProfileModal = function () {
        $('#dlg-profile').modal('show');
    };

    this.submitChangePassword = function () {
        var frm = $('#form-profile');
        $("#profile-msg-error").addClass('hidden');
        $('#profile-btn-submit').prop('disabled', true);
        $('#profile-spinner').removeClass("hide");
        var formData = {
            password: $('#profile-password').val(),
            confirmPassword: $('#profile-confirmPassword').val(),
        };
        var request = $.ajax({
            url: frm.attr('action'),
            method: "POST",
            data: formData
        });

        request.always(function (xhr, textStatus, errorThrown) {
            $('#profile-btn-submit').prop('disabled', false);
            $('#profile-spinner').addClass("hide");
        });
        request.done(function (response) {
            if (response.success === false) {
                Utils.showError('#profile-msg-error', response.messages[0]);
            }
            else {
                $('#dlg-profile').modal('hide');
            }
        });

        request.fail(function (xhr, textStatus) {
            try {
                Utils.showError('#profile-msg-error', xhr.responseJSON.message);
            }
            catch (err) {
                Utils.showError('#profile-msg-error', 'A fatal error occurred');
            }
        });

    };

};


$(document).ready(function () {
    new MainLayout().init();
});
