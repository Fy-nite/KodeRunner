
module instal
    use iso_fortran_env, only: OUTPUT_UNIT
    use iso_c_binding, only: c_f_pointer, c_ptr, c_char
    implicit none
    interface

    end interface

    contains
        subroutine help() bind(c, name="help")
            PRINT *, ":3"
            PRINT *, "'help' displays the help text"
            PRINT *, "'patchnotes' displays the patch notes"
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
            PRINT *, ""
        end subroutine help

    
end module instal

