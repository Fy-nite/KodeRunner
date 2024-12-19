
module instal
    use iso_fortran_env, only: OUTPUT_UNIT
    use iso_c_binding, only: c_f_pointer, c_ptr, c_char
    implicit none
    interface
        !  C interface to the download patchnotes function

    end interface

    contains
        subroutine fortran_test() bind(c, name="fortran_test")
            PRINT *, "Hello from Fortran"
        end subroutine fortran_test

    
end module instal

