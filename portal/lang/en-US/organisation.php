<?php

return [
    'title' => 'Organisation',
    'subtitle' => 'Your DaftarX team members. Roles control what each member can do.',
    'role' => [
        'Owner' => 'Owner',
        'BillingAdmin' => 'Billing admin',
        'SupportAdmin' => 'Support admin',
        'ReadOnly' => 'Read-only',
    ],
    'role_description' => [
        'Owner' => 'Full access (invite, billing, licences, cancel)',
        'BillingAdmin' => 'Invoices + subscriptions + upgrades',
        'SupportAdmin' => 'Support tickets only',
        'ReadOnly' => 'View-only (no edits)',
    ],
    'members_list' => [
        'title' => 'Current members',
        'empty' => 'No members yet. Invite a partner or accountant using the form below.',
        'columns' => [
            'name' => 'Name',
            'email' => 'Email',
            'role' => 'Role',
            'joined_at' => 'Joined',
            'status' => 'Status',
            'actions' => 'Actions',
        ],
        'status' => [
            'active' => 'Active',
            'pending' => 'Pending acceptance',
            'revoked' => 'Revoked',
        ],
        'revoke_button' => 'Revoke',
        'revoke_confirm' => 'Sure you want to remove :name from the organisation? They will be signed out immediately and cannot sign back in.',
    ],
    'pending_section_title' => 'Pending invitations',
    'pending_columns' => [
        'email' => 'Email',
        'role' => 'Role',
        'invited_by' => 'Invited by',
        'expires_at' => 'Expires',
    ],
    'invite_form' => [
        'title' => 'Invite a new member',
        'email_label' => 'Email',
        'role_label' => 'Role',
        'display_name_label' => 'Name (optional)',
        'locale_label' => 'Invitation language',
        'submit' => 'Send invitation',
    ],
    'invitation_sent_flash' => 'Invitation sent to :email. Expires in 7 days.',
    'member_removed_flash' => 'Member removed. They will be signed out on their next page refresh.',

    'accept' => [
        'title' => 'Accept invitation',
        'subtitle' => 'You have been invited to join :org as :role.',
        'set_password_label' => 'Password (at least 12 chars + digit + upper + lower)',
        'display_name_label' => 'Your name',
        'existing_account_note' => 'This email already has a DaftarX account. Sign in with your existing password.',
        'submit_new' => 'Create account and accept',
        'submit_existing' => 'Accept invitation',
    ],
    'accept_welcome_flash' => 'Welcome! Invitation accepted; you can now manage the organisation.',
    'expired_title' => 'Invitation expired',
    'expired_body' => 'This link expired. Ask the Owner to send a fresh invitation.',
    'already_used_title' => 'Invitation already used',
    'already_used_body' => 'This link was already accepted. To access the portal sign in at /login.',
];
